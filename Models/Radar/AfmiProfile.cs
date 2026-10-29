using System;
using System.Collections.Generic;

namespace AFMI_Montage_projet_2.Models.Radar
{
    /// <summary>
    /// بطاقة تعريف جمعية الغابة النموذجية لإفران (AFMI) أو المنظمة الحاملة للمشروع.
    /// تُحفظ في afmi_profile.json وتُربط مع مختلف مكونات البرنامج (Cadrage, Radar, Exports, AI Grant Writer).
    /// </summary>
    public class AfmiProfile
    {
        #region 1. Identité Institutionnelle & Coordonnées Légales

        public string OrganizationName { get; set; } = "Association Forêt Modèle Ifrane (AFMI)";
        public string Acronym { get; set; } = "AFMI";
        public string CreationDate { get; set; } = "2010";
        public string LegalStatus { get; set; } = "Association marocaine à but non lucratif régie par le dahir n° 1-58-376";
        public string RegistrationNumber { get; set; } = "Récépissé définitif n° 2010/42 - Province d'Ifrane";
        public string Headquarters { get; set; } = "Ifrane, Région Fès-Meknès, Royaume du Maroc";
        public string Address { get; set; } = "Avenue de la Marche Verte, B.P. 120, Ifrane 53000";
        public string Phone { get; set; } = "+212 (0) 5 35 56 60 00";
        public string Email { get; set; } = "contact@foretmodele-ifrane.ma";
        public string Website { get; set; } = "www.foretmodele-ifrane.ma";
        public string FacebookUrl { get; set; } = "https://www.facebook.com/foretmodeleifrane";
        public string LinkedInUrl { get; set; } = "https://www.linkedin.com/company/afmi-ifrane";
        public string TwitterUrl { get; set; } = "";
        public string InstagramUrl { get; set; } = "";
        public string YouTubeUrl { get; set; } = "";
        public string NotebookLmUrl { get; set; } = "https://notebooklm.google.com";
        public List<string> AnalyzedSourcesHistory { get; set; } = new();
        public string LegalRepresentative { get; set; } = "Président du Conseil d'Administration";

        #endregion

        #region 2. Vision, Mission, Axes Stratégiques & Valeurs

        public string Mission { get; set; } = "Gestion durable et concertée des écosystèmes forestiers et pastoraux du Moyen Atlas, conciliation du développement socio-économique local et de la conservation de la biodiversité.";
        public string Vision { get; set; } = "Faire du Moyen Atlas un pôle d'excellence et un modèle international de résilience écologique, où les communautés locales prospèrent en harmonie avec les forêts méditerranéennes.";
        public string CoreValues { get; set; } = "Concertation multi-acteurs, Intégrité fiduciaire, Préservation de la biodiversité, Inclusion sociale des femmes et des jeunes, Innovation écologique et durabilité.";

        public string StrategicAxes { get; set; } = 
            "Axe 1. Renforcement des capacités de gouvernance\n" +
            "• Renforcement du partenariat : Fonctionnement des organes statutaires, mise en place de comités techniques et planification concertée.\n" +
            "• Gouvernance des acteurs locaux : Appui aux associations de gestion sylvo-pastorale (AGS), coopératives et ONG locales dans le montage, la gestion et la conduite de leurs projets en partenariat.\n\n" +
            "Axe 2. Gestion des ressources naturelles\n" +
            "• Aménagement et gestion des parcours : Développement de projets sylvo-pastoraux et élaboration de chartes pastorales.\n" +
            "• Gestion forestière : Promotion de la foresterie participative, de la sylviculture adaptative et de la gestion contractuelle avec les usagers.\n" +
            "• Conservation de la biodiversité et lutte contre la pollution : Sensibilisation, protection des espèces menacées (comme le cèdre et le singe magot) et gestion contractuelle des sites sensibles.\n" +
            "• Gestion de l'eau et des ressources hydrauliques : Aménagement des zones humides, traitement des eaux usées et amélioration de l'efficacité de l'irrigation.\n\n" +
            "Axe 3. Énergie et habitat\n" +
            "• Gestion durable des sources d'énergie : Utilisation rationnelle et efficiente du bois de chauffe, développement de plantations énergétiques et promotion des énergies renouvelables (notamment solaires).\n" +
            "• Économies d'énergie et amélioration de l'habitat : Sensibilisation à l'isolation thermique du bâti et à l'efficacité énergétique afin de réduire les coûts ménagers et la pression sur les forêts.\n\n" +
            "Axe 4. Diversification et amélioration des revenus\n" +
            "• Organisation des filières et renforcement des acteurs économiques : Formation des producteurs, accompagnement des organisations locales et structuration des filières productives.\n" +
            "• Valorisation et promotion des produits du terroir : Démarches de qualification, labellisation et commercialisation (ex. safran, produits forestiers non ligneux).\n" +
            "• Promotion du tourisme durable : Intégration du patrimoine naturel et culturel dans l'offre touristique locale (Réseau Cèdre, écotourisme).\n\n" +
            "Axe 5. Communication et mise en réseau\n" +
            "• Information et sensibilisation locale : Organisation de campagnes d'information et d'ateliers de sensibilisation auprès des populations et scolaires.\n" +
            "• Communication événementielle et externe : Organisation de manifestations culturelles/environnementales (ex. Journée de la poésie Amazigh) et diffusion d'outils d'information (site web, brochures).\n" +
            "• Vie en réseau : Maillage entre acteurs locaux, partage d'expériences au niveau national et intégration active au Réseau Méditerranéen (RMFM), Africain (RAFM) et International des Forêts Modèles (RIFM).";

        public string StrategicObjectives { get; set; } = "1. Préserver et restaurer les écosystèmes forestiers (cédraies, chênaies) et les zones humides du Moyen Atlas.\n2. Promouvoir l'économie verte locale, le sylvopastoralisme durable et l'écotourisme solidaire.\n3. Renforcer la résilience climatique des populations rurales et des bassins versants.\n4. Développer l'éducation environnementale, la recherche appliquée et la participation communautaire.";

        public List<string> GeographicZones { get; set; } = new()
        {
            "Province d'Ifrane",
            "Moyen Atlas",
            "Parc National d'Ifrane",
            "Bassin hydraulique du Sebou",
            "Maroc"
        };

        public List<string> PreferredThemes { get; set; } = new()
        {
            "Cédraies et forêts méditerranéennes",
            "Conservation de la biodiversité et espèces endémiques",
            "Ressources en eau et zones humides (lacs et sources)",
            "Changement climatique et résilience écologique",
            "Sylvopastoralisme et gestion durable des parcours",
            "Écotourisme et valorisation des produits du terroir",
            "Autonomisation des femmes et coopératives locales",
            "Éducation environnementale et participation communautaire"
        };

        #endregion

        #region 3. Historique & Trajectoire

        public string HistoryTrajectory { get; set; } = "Créée en 2010 dans le cadre du Réseau International des Forêts Modèles (RIFM) et du Réseau Méditerranéen, l'Association Forêt Modèle Ifrane (AFMI) a été pionnière au Maroc dans l'instauration d'une gouvernance territoriale partagée regroupant forestiers, élus locaux, universitaires, éleveurs et acteurs de la société civile. En plus d'une décennie d'action continue, l'AFMI s'est affirmée comme une plateforme consultative incontournable et un acteur de premier plan pour le développement durable territorial.";

        #endregion

        #region 4. Réalisations & Bilan d'Impact (Track Record)

        public string KeyAchievements { get; set; } = "• Restauration et mise en défens participative de plus de 1 200 hectares de cédraies et parcours dégradés.\n• Structuration et renforcement des capacités de 18 coopératives féminines et d'éleveurs (apiculture, plantes aromatiques, artisanat).\n• Aménagement de circuits d'écotourisme et sentiers pédagogiques au sein du Parc National d'Ifrane.\n• Installation de points d'eau pastoraux solaires et aménagement de sources préservant les zones humides.\n• Sensibilisation de plus de 15 000 jeunes et écoliers aux enjeux de conservation de la biodiversité.";
        public string ImpactFigures { get; set; } = "+25 000 bénéficiaires directs et indirects | 1 200 ha restaurés | 18 coopératives soutenues | 2,5 M€ de subventions gérées sans réserve d'audit.";
        public string RecognitionsAwards { get; set; } = "Membre officiel accrédité du Réseau International des Forêts Modèles (RIFM / IMFN) ; Reconnaissance régionale pour l'excellence en gouvernance concertée et innovation écologique.";

        #endregion

        #region 5. Capacités & Ressources Organisationnelles

        public string HumanResourcesCapacity { get; set; } = "Équipe permanente pluridisciplinaire comprenant : 1 Directeur exécutif, 1 Responsable Administratif et Financier (RAF), 2 Ingénieurs agronomes/forestiers, 1 Spécialiste Suivi-Évaluation & SIG, 1 Chargé de mobilisation communautaire. Appui régulier d'un réseau d'experts universitaires et d'un comité scientifique consultatif.";
        public string FinancialManagementCapacity { get; set; } = "Système de gestion financière et administrative conforme aux exigences des bailleurs internationaux : manuel de procédures actualisé, séparation stricte des fonctions, procédure de double signature, tenue d'une comptabilité analytique sur logiciel dédié. Comptes annuels certifiés par un Commissaire aux Comptes indépendant. Audits réguliers par les bailleurs sans aucune réserve.";
        public string LogisticalMaterialCapacity { get; set; } = "Siège administratif opérationnel à Ifrane, salle de formation équipée de 40 places, 2 véhicules tout-terrain 4x4 pour les interventions de terrain, matériel informatique et de cartographie SIG, drones de surveillance écologique, stations météo autonomes.";

        #endregion

        #region 6. Expérience Bailleurs & Partenariats

        public List<string> PreferredDonors { get; set; } = new()
        {
            "GEF",
            "PNUD",
            "Union Européenne",
            "Banque Mondiale",
            "AFD",
            "UICN",
            "FAO",
            "CEPF"
        };

        public string PastProjectsReferences { get; set; } = "1. Projet PNUD/FEM : 'Préservation de la biodiversité et résilience des cédraies du Moyen Atlas' (Budget : 450 000 USD, 2018-2021) - Rôle : Chef de file.\n2. Programme UE / Partenariat Territorial : 'Appui aux filières vertes et coopératives féminines de montagne' (Budget : 380 000 EUR, 2020-2023) - Rôle : Porteur principal.\n3. Initiative CEPF / UICN : 'Conservation des zones humides et des espèces menacées d'Ifrane' (Budget : 120 000 USD, 2022-2024).\n4. Partenariat National : Agence Nationale des Eaux et Forêts (ANEF), Université Al Akhawayn, Parc National d'Ifrane.";
        public string StrategicPartnerships { get; set; } = "Agence Nationale des Eaux et Forêts (ANEF), Province d'Ifrane, Secrétariat du Réseau International des Forêts Modèles (Canada), Université Al Akhawayn d'Ifrane, Université Sidi Mohamed Ben Abdellah de Fès, Parc National d'Ifrane, Réseau Méditerranéen des Forêts Modèles.";
        public string TechnicalExpertiseSummary { get; set; } = "Ingénierie écologique et sylvopastorale, modélisation territoriale participative, gestion adaptative face au stress hydrique, renforcement organisationnel des coopératives rurales, cartographie SIG et télédétection appliquée.";

        #endregion

        #region 7. Déclaration Synthétique de Capacité (Capacity Statement)

        public string DonorCapacityStatement { get; set; } = "Forte de plus de 14 années d'ancrage territorial éprouvé dans le Moyen Atlas marocain, l'Association Forêt Modèle Ifrane (AFMI) réunit une gouvernance transparente, une capacité de gestion fiduciaire irréprochable et une expertise technique de référence en matière de restauration écologique et d'autonomisation communautaire. Membre du Réseau International des Forêts Modèles et partenaire de confiance des grands bailleurs multilatéraux (PNUD, UE, FEM, CEPF), l'AFMI dispose de toutes les garanties institutionnelles, humaines et matérielles pour mener à bien des programmes de développement durables, mesurables et audités selon les plus hauts standards internationaux.";

        #endregion
    }
}
