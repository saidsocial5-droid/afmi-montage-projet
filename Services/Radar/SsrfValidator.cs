using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>
    /// مدقق حماية SSRF (Server-Side Request Forgery Defense):
    /// يمنع استدعاء شبكات داخلية أو localhost أو منافذ غير معتادة أو عناوين خاصة.
    /// </summary>
    public static class SsrfValidator
    {
        private static readonly int[] AllowedPorts = { 80, 443, 8080, 8443 };

        public static (bool IsValid, string Reason) ValidateUrl(string urlString)
        {
            if (string.IsNullOrWhiteSpace(urlString))
                return (false, "L'URL est vide.");

            if (!Uri.TryCreate(urlString, UriKind.Absolute, out var uri))
                return (false, "Format d'URL invalide.");

            // 1. Uniquement http et https
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return (false, "Seuls les protocoles http et https sont autorisés.");

            // 2. Vérification des ports autorisés
            if (!AllowedPorts.Contains(uri.Port))
                return (false, $"Le port {uri.Port} n'est pas autorisé (seuls 80, 443, 8080, 8443 sont acceptés).");

            string host = uri.DnsSafeHost.Trim().ToLowerInvariant();

            // 3. Rejet localhost et alias
            if (host == "localhost" || host == "localhost.localdomain" || host == "ip6-localhost" || host.EndsWith(".localhost") || host.EndsWith(".local"))
                return (false, "L'accès à localhost ou aux domaines locaux est strictement interdit.");

            // 4. Résolution DNS et vérification des adresses IP
            try
            {
                IPAddress[] ips;
                if (IPAddress.TryParse(host, out var directIp))
                {
                    ips = new[] { directIp };
                }
                else
                {
                    ips = Dns.GetHostAddresses(host);
                }

                if (ips.Length == 0)
                    return (false, "Impossible de résoudre l'adresse IP de l'hôte.");

                foreach (var ip in ips)
                {
                    if (IsPrivateOrRestrictedIp(ip))
                    {
                        return (false, $"L'adresse résolue ({ip}) pointe vers un réseau privé ou restreint (Protection SSRF).");
                    }
                }
            }
            catch (SocketException ex)
            {
                // Si la résolution échoue en mode hors-ligne, mais que l'URL est sémantiquement sûre
                // on vérifie au moins que ce n'est pas une IP directe interdite
                if (IPAddress.TryParse(host, out var directIp) && IsPrivateOrRestrictedIp(directIp))
                {
                    return (false, $"L'adresse IP ({directIp}) est réservée ou privée.");
                }
                // Si DNS introuvable, laisser passer pour le test ou rapporter l'erreur
                return (false, $"Erreur de résolution DNS : {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur de validation de l'hôte : {ex.Message}");
            }

            return (true, string.Empty);
        }

        public static bool IsPrivateOrRestrictedIp(IPAddress ip)
        {
            // Adresse IPv4 encapsulée dans IPv6 (::ffff:127.0.0.1, NAT64 64:ff9b::/96) : vérifier l'IPv4 réelle
            if (ip.IsIPv4MappedToIPv6) return IsPrivateOrRestrictedIp(ip.MapToIPv4());
            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                byte[] v6 = ip.GetAddressBytes();
                bool isNat64 = v6[0] == 0x00 && v6[1] == 0x64 && v6[2] == 0xFF && v6[3] == 0x9B && v6.Skip(4).Take(8).All(b => b == 0);
                if (isNat64) return IsPrivateOrRestrictedIp(new IPAddress(v6.Skip(12).ToArray()));
            }

            if (IPAddress.IsLoopback(ip)) return true;

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] bytes = ip.GetAddressBytes();
                byte b0 = bytes[0];
                byte b1 = bytes[1];

                // 0.0.0.0/8
                if (b0 == 0) return true;

                // 10.0.0.0/8 (Private Network)
                if (b0 == 10) return true;

                // 127.0.0.0/8 (Loopback)
                if (b0 == 127) return true;

                // 169.254.0.0/16 (Link Local / AWS Metadata)
                if (b0 == 169 && b1 == 254) return true;

                // 100.64.0.0/10 (Shared Address Space / CGNAT)
                if (b0 == 100 && b1 >= 64 && b1 <= 127) return true;

                // 192.0.0.0/24 (IETF Protocol Assignments) et 198.18.0.0/15 (Benchmarking)
                if (b0 == 192 && b1 == 0 && bytes[2] == 0) return true;
                if (b0 == 198 && (b1 == 18 || b1 == 19)) return true;

                // 172.16.0.0/12 (Private Network: 172.16.0.0 - 172.31.255.255)
                if (b0 == 172 && b1 >= 16 && b1 <= 31) return true;

                // 192.168.0.0/16 (Private Network)
                if (b0 == 192 && b1 == 168) return true;

                // 224.0.0.0/4 (Multicast)
                if (b0 >= 224 && b0 <= 239) return true;

                // 240.0.0.0/4 (Reserved)
                if (b0 >= 240) return true;

                // 255.255.255.255 (Broadcast)
                if (b0 == 255 && b1 == 255 && bytes[2] == 255 && bytes[3] == 255) return true;
            }
            else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast) return true;

                byte[] bytes = ip.GetAddressBytes();

                // ::1 (Loopback)
                if (ip.Equals(IPAddress.IPv6Loopback) || ip.Equals(IPAddress.IPv6None) || ip.Equals(IPAddress.IPv6Any)) return true;

                // fc00::/7 (Unique Local Address)
                if ((bytes[0] & 0xFE) == 0xFC) return true;

                // fe80::/10 (Link-Local)
                if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true;
            }

            return false;
        }
    }
}
