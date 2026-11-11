namespace Game.Core.Model.Character;

public sealed class ReputationState
{
    public int Good { get; private set; }

    public int Bad { get; private set; }

    public int Total => checked(Math.Abs(Good) + Math.Abs(Bad));

    public bool Change(int goodDelta, int badDelta)
    {
        if (goodDelta == 0 && badDelta == 0)
        {
            return false;
        }

        Good = checked(Good + goodDelta);
        Bad = checked(Bad + badDelta);
        return true;
    }

    public void Set(int good, int bad)
    {
        Good = good;
        Bad = bad;
    }
}
