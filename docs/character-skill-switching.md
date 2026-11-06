# 角色技能切换

角色面板已接入技能切换。`CharacterPanel` 根据技能类型调用 `CharacterService`，由领域对象修改状态；发生变更后应用层发布 `CharacterChangedEvent`，角色面板与名册等订阅者刷新显示。重复设置相同状态不发布变更事件。

| 技能类型 | 应用层入口 | 状态 |
| --- | --- | --- |
| 外功 | `SetExternalSkillActive` | 独立激活开关 |
| 特殊技能 | `SetSpecialSkillActive` | 独立激活开关 |
| 招式 | `SetFormSkillActive` | 按来源技能和招式 ID 独立启停 |
| 内功 | `EquipInternalSkill` | 切换当前装备内功 |
| 使用武学栏位（外功） | `SetUsedSkillSlot` | 10 格固定栏位，同一技能只占一格，栏位序号即战场顺序 |

外功和特殊技能切换通过现有实例修改激活态，保留等级、经验和冷却等运行状态；不为此创建新的技能服务或替换技能实例。外功被动 affix 不随激活态关闭。内功装备变化由 `CharacterInstance` 重建角色快照，使依赖装备内功的属性与天赋生效。

使用武学栏位是独立的出战顺序，与 `IsActive` 互不影响。`CharacterInstance.UsedSkillSlots` 固定 `UsedSkillSlotCount`（10）项，元素为外功 id 或 null，第 N 项的序号就是战场技能列表中的顺序。`SetUsedSkillSlot(index, skillId)` 是唯一写入口：非空 id 必须是角色已拥有的外功（否则抛 `InvalidOperationException`），写入前会先把该 id 从其它栏位清掉，因此同一技能不会重复占格；越界索引抛 `ArgumentOutOfRangeException`，返回 false 表示状态未变化。存档写入 `CharacterRecord.UsedSkillSlots`，恢复时去重并丢弃未拥有或越过栏位总数的条目。

招式的启停状态由来源外功或内功持有，禁用列表保存为 `DisabledFormSkillIds`。`CharacterMapper` 在对应的 `ExternalSkillRecord`、`InternalSkillRecord` 中写入并恢复该列表；它不是仅用于显示的临时勾选状态。招式最终能否使用还受来源技能状态等领域规则约束。

实现入口：

- [CharacterService](../src/Game.Application/CharacterService.cs)：应用层操作与角色变更事件。
- [CharacterInstance](../src/Game.Core/Model/Character/CharacterInstance.cs)：激活、装备与快照更新。
- [CharacterMapper](../src/Game.Core/Model/Character/CharacterMapper.cs)：存档转换。
- [CharacterPanel](../src/Game.Godot/UI/Character/CharacterPanel.cs)：技能卡点击与界面刷新。
- [SkillTab](../src/Game.Godot/UI/Skills/SkillTab.cs)：使用武学 10 格栏位；点栏位打开右侧技能列表，点技能写入该栏位（互斥由领域对象保证）。
- [BattleSkillCatalog](../src/Game.Core/Battle/BattleSkillCatalog.cs)：按栏位序号排列战场可选用技能。
