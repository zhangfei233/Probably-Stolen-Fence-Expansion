# 销赃人 / Fence Expansion 0.3.2

为 Probably Stolen（深空当铺）扩展王尔德的销赃人技能页。包含15项升级、物资箱扩容与隐匿、钥匙卡品质、黑市交易、大盗与末影人，以及异界消耗品。支持中英文，随游戏语言切换。

Fence Expansion adds 15 upgrades, enhanced supply crates and keycards, black-market visitors, the Master Thief, Enderman, and otherworldly consumables to Probably Stolen. Chinese and English are supported.

## 从源码编译 / Build

需要 PowerShell 7、.NET SDK（支持 netstandard2.1）以及自行安装的正版游戏。本仓库不包含游戏、游戏程序集或第三方模组。

1. 下载源码并打开项目根目录。
2. 指定本机游戏的 `Probably Stolen_Data/Managed` 目录运行：

```powershell
pwsh -NoProfile -File ./build.ps1 -GameManaged "D:/Steam/steamapps/common/Probably Stolen/Probably Stolen_Data/Managed"
```

3. 输出位于 `dist/FenceExpansion/`。无需手动修改或反编译游戏，也不需要启动游戏来编译。

构建时读取根目录 `skill.json`，生成技能定义并复制素材。发行配置会自动移除内部 `effect` 字段，保留可调整的 `cost`。游戏DLL仅作编译引用，不会打包进模组。

PowerShell 7 and a .NET SDK are required. Pass the Managed directory of your own game installation using `-GameManaged`. The package is written to `dist/FenceExpansion/`; game assemblies are referenced, never bundled.

## 安装与配置

将整个 `FenceExpansion` 文件夹放入游戏的 `Mods` 文件夹，在游戏模组菜单启用“销赃人 / Fence Expansion”。保留 `FenceExpansion.dll`、`manifest.xml`、`skill.json` 和 `Textures` 目录。

编辑安装目录内 `skill.json` 的 `cost` 可调整各技能价格，重新加载模组后读取。`dynamic-code` 是游戏安全扫描对 Harmony 补丁要求的权限声明。

## 技能

技能页为五行三列。前四行各自逐级解锁；第五行三项独立。金灯大盗还需要基础页供货商分支的“周一特供”。括号内为默认信用点价格。

| 行 | 技能1 | 技能2 | 技能3 |
|---|---|---|---|
| 1 | 更大的箱子（300） | 极限容量（700） | 隐匿改装（800） |
| 2 | 查缺补漏（150） | 声名鹊起（600） | 稳定的销路（1100） |
| 3 | 地下开锁匠（300） | 夜间降温（600） | 电子屏障（1000） |
| 4 | 金灯大盗（888） | 异界伙伴（600） | 异界美味（800） |
| 5 | 黑帮大佬（1200） | 黑市头牌（3000） | 上层名人（700） |

## 主要内容

- 物资箱扩容即时生效：已开启的箱子只扩大空间；未开启的按升级后的空间生成战利品。隐匿改装保护已开箱中的物品免受盘查。
- 钥匙卡拥有普通、稀有、史诗、传说四种品质，高品质卡提高同类战利品的价值倾向。
- 黑市出现概率可突破原上限；超过100%的部分转化为对应角色的折扣，最多50%。黑市头牌使每盏灯分别尝试吸引额外访客。
- 大盗按顺序提供额外奖励箱、70%概率奢侈品箱、10%概率神经核心模组，再用基础价值大于150的正常商品填满货架。池中包含全部原版超科技商人商品。奢侈品箱保底一张随机品质指挥卡。
- 末影人的1×1名片可双击查看0058号码。每天可预约次日来访，一次收购一箱，预算为 `max(1000, 2000 + 黑市声望 × 15)`，沿用原版议价与预算封顶。
- 附魔之瓶属于烟酒类与奢侈品，摄入永久幸运+5。金苹果授予一天的“金苹果的祝福”，实际销售收入+25%，不叠加，下次睡眠到期。
- 向治安部举报大盗或末影人，按原版误报无辜顾客处理；两人来访期间不能使用霰弹枪。
- 上层名人保证展示奢侈品时原版奢侈品收购商每日到访。其他分支提供卡片兑换、赃物清理和阵营声望服务。

## 源码结构

- `src/FenceExpansion/`：模组源码与清单。
- `assets/`：技能、物品、名片与角色素材。
- `skill.json`：开发规格、双语文本、默认价格及布局。
- `tools/GenerateSkills.ps1`：构建所需的技能代码生成器。
- `build.ps1`：编译和打包入口。

`.gitignore` 仅允许公开源码、素材和构建文档；游戏副本、备份、测试、反编译研究资料、导出目录、编译产物及本地工具均被排除。高价商品目录的生成结果保存在源码中，普通编译不依赖本地研究资料。

## 日志与卸载

F8打开控制台，可运行 `mod-scan fence_expansion` 查看扫描结果。日志通常位于 `%USERPROFILE%/AppData/LocalLow/Questing Goose Studio/Probably Stolen/Player.log`。

模组会向存档加入升级和自定义物品。卸载前先移出扩容箱超出原尺寸的内容，并处理自定义物品；保留存档备份便于恢复。
