using System;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Requests
{
    /// <summary> Identifies a public alias or an alias generated while lowering one edit action. </summary>
    internal abstract class RequestLocalAliasIdentity : IEquatable<RequestLocalAliasIdentity>
    {
        private RequestLocalAliasIdentity (UcliPlanAlias alias)
        {
            Alias = alias ?? throw new ArgumentNullException(nameof(alias));
        }

        public UcliPlanAlias Alias { get; }

        public static RequestLocalAliasIdentity FromPublicAlias (UcliPlanAlias alias)
        {
            return new PublicAliasIdentity(alias);
        }

        public static EditActionAliasIdentity ForEditAction (
            IpcExecuteStepId stepId,
            int branchIndex,
            UcliPlanAlias alias)
        {
            return new EditActionAliasIdentity(stepId, branchIndex, alias);
        }

        public bool Equals (RequestLocalAliasIdentity? other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || GetType() != other.GetType() || !Alias.Equals(other.Alias))
            {
                return false;
            }

            return this is not EditActionAliasIdentity editAction
                || (other is EditActionAliasIdentity otherEditAction
                    && editAction.StepId.Equals(otherEditAction.StepId)
                    && editAction.BranchIndex == otherEditAction.BranchIndex);
        }

        public override bool Equals (object? obj)
        {
            return obj is RequestLocalAliasIdentity other && Equals(other);
        }

        public override int GetHashCode ()
        {
            unchecked
            {
                var hashCode = this is PublicAliasIdentity ? 0 : 1;
                if (this is EditActionAliasIdentity editAction)
                {
                    hashCode = (hashCode * 397) ^ editAction.StepId.GetHashCode();
                    hashCode = (hashCode * 397) ^ editAction.BranchIndex;
                }

                return (hashCode * 397) ^ Alias.GetHashCode();
            }
        }

        private sealed class PublicAliasIdentity : RequestLocalAliasIdentity
        {
            public PublicAliasIdentity (UcliPlanAlias alias)
                : base(alias)
            {
            }
        }

        internal sealed class EditActionAliasIdentity : RequestLocalAliasIdentity
        {
            public EditActionAliasIdentity (
                IpcExecuteStepId stepId,
                int branchIndex,
                UcliPlanAlias alias)
                : base(alias)
            {
                StepId = stepId ?? throw new ArgumentNullException(nameof(stepId));
                if (branchIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(branchIndex), branchIndex, "Branch index must not be negative.");
                }

                BranchIndex = branchIndex;
            }

            public IpcExecuteStepId StepId { get; }

            public int BranchIndex { get; }
        }
    }
}
