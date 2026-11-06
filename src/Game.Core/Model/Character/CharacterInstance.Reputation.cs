namespace Game.Core.Model.Character;

public sealed partial class CharacterInstance
{
    public ReputationState Reputation { get; } = new();

    public bool ChangeReputation(int goodDelta, int badDelta) => Reputation.Change(goodDelta, badDelta);

    public void RestoreReputation(int goodReputation, int badReputation) =>
        Reputation.Set(goodReputation, badReputation);
}
