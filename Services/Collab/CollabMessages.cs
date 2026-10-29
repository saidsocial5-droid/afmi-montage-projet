using System;
using AFMI_Montage_projet_2.Models;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace AFMI_Montage_projet_2.Services.Collab
{
    public class ProposalCreatedMessage : ValueChangedMessage<Contribution>
    {
        public ProposalCreatedMessage(Contribution value) : base(value) { }
    }

    public class ProposalUpdatedMessage : ValueChangedMessage<Contribution>
    {
        public ProposalUpdatedMessage(Contribution value) : base(value) { }
    }

    public class VoteSubmittedMessage : ValueChangedMessage<Vote>
    {
        public VoteSubmittedMessage(Vote value) : base(value) { }
    }

    public class TeamConfigUpdatedMessage : ValueChangedMessage<string>
    {
        public TeamConfigUpdatedMessage(string teamId) : base(teamId) { }
    }
}
