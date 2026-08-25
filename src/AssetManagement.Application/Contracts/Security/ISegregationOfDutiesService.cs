namespace AssetManagement.Application.Contracts
{
    public interface ISegregationOfDutiesService
    {
        void EnsureActorIsNotRequester(string requesterUserId, string actorUserId, string processCode);

        bool WouldViolateSegregation(string requesterUserId, string actorUserId);
    }
}
