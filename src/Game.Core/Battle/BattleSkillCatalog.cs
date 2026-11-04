using Game.Core.Model.Skills;

namespace Game.Core.Battle;

public static class BattleSkillCatalog
{
    public static IReadOnlyList<SkillInstance> CollectSelectableSkills(BattleUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);

        var character = unit.Character;
        var externalSkills = character.GetExternalSkills();
        var internalSkills = character.GetInternalSkills();
        var skills = character.GetSpecialSkills()
            .Where(static skill => skill.IsActive)
            .Cast<SkillInstance>()
            .Concat(externalSkills
                .SelectMany(static skill =>
                    (skill.IsActive
                        ? new[] { (SkillInstance)skill }
                        : [])
                    .Concat(skill.GetFormSkills().Where(static formSkill => formSkill.IsActive))))
            .Concat(internalSkills
                .SelectMany(static skill => skill.GetFormSkills().Where(static formSkill => formSkill.IsActive)))
            .ToList();

        return OrderByUsedSkillSlots(skills, character.UsedSkillSlots);
    }

    private static IReadOnlyList<SkillInstance> OrderByUsedSkillSlots(
        IReadOnlyList<SkillInstance> skills,
        IReadOnlyList<string?> usedSkillSlots)
    {
        var slotOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < usedSkillSlots.Count; index++)
        {
            if (usedSkillSlots[index] is { } skillId)
            {
                slotOrder[skillId] = index;
            }
        }

        if (slotOrder.Count == 0)
        {
            return skills;
        }

        return skills
            .OrderBy(skill => slotOrder.TryGetValue(skill.Id, out var slotIndex) ? slotIndex : int.MaxValue)
            .ToList();
    }
}
