
using Game.Core.Story;

namespace Game.Core.Battle;

public sealed record BattleEventScript
(
    int Version,
    IReadOnlyList<BattleMainSegment> Segments)
{
    public const int CurrentVersion = 3;
}

    public sealed record BattleMainSegment(
    string Name,
    IReadOnlyList<BattleSmallSegment> BattleSmallSegments);


    public sealed record BattleSmallSegment(
    IReadOnlyList<BranchCase> Cases,
    IReadOnlyList<BattleStep>? Fallback);

    public sealed record BranchCase(
    ParsedExpression When,
    IReadOnlyList<BattleStep> Steps);
   
   public abstract record BattleStep : Step;
   public sealed record ReturnStep : BattleStep;
   public sealed record JumpStep(
    string Target) : BattleStep;

    public sealed record CallStep(
    string Target) : BattleStep;
   public sealed record SetVariableStep(
    string Target,
    ParsedExpression Value) : BattleStep;
   public sealed record DeleteVariableStep(
    string Target) : BattleStep;
    public sealed record BattleCommandStep(
        ParsedCall Call) : BattleStep;
    //读取story
    public sealed record BattleInvokeStory(
        String StoryId) : BattleStep;
    public sealed record DialogueStep(
    string Speaker,
    string Text) : BattleStep;
    public sealed record CommandStep(
    ParsedCall Call) : BattleStep;




