# 风车扇叶转动需求拆分与使用说明

> 对应需求："比如那个风车的扇叶转动"。
>
> 目标是给场景里的风车增加持续转动表现。该需求只属于表现层，不改变风路、计分、建筑合法性等领域规则。

---

## 1. 适用资源

当前项目里已有风车 Prefab：

- `Assets/Resources/Prefab/Element/giantWindmill.prefab`

推荐新增脚本目录：

- `Assets/Script/View/Environment/WindmillBladeRotator.cs`

如果后续要做更多环境表现，比如树叶摆动、水面、旗帜、雾效，也统一放到：

- `Assets/Script/View/Environment/`

---

## 2. 需求拆分

### 2.1 基础版本

实现一个 `WindmillBladeRotator` 组件，挂在风车扇叶的旋转轴节点上。

组件职责：

- 每帧绕本地轴旋转扇叶。
- 支持配置旋转轴，例如 `Local X / Local Y / Local Z`。
- 支持配置转速，单位建议用 `RPM` 或 `Degrees Per Second`。
- 支持反向旋转。
- 支持随机初始角度，避免多个风车同步转动太机械。

基础表现验收：

- 进入 Play Mode 后，风车扇叶能稳定连续旋转。
- 调整转速参数后，转动速度立即变化。
- 复制多个风车时，扇叶初始角度可错开。
- 关闭组件后，扇叶停止转动。

### 2.2 受风力影响版本

在基础版本可用后，再接入风系统表现数据。

行为建议：

- 无风时低速或停止。
- 风力越高，扇叶转速越快。
- 风向与风车朝向接近时高速；背风或侧风时降速。
- 这个联动只影响动画表现，不反写领域层风数据。

推荐参数：

| 参数 | 建议默认值 | 说明 |
|---|---:|---|
| `idleRpm` | `4` | 无风或未接入风系统时的慢速待机 |
| `minWindRpm` | `20` | 有风时最低转速 |
| `maxWindRpm` | `90` | 满风力时最高转速 |
| `axis` | `Local Z` | 具体按模型扇叶轴向调整 |
| `randomStartAngle` | `true` | 多个风车错帧 |
| `smoothTime` | `0.3` | 风力变化时的速度缓动 |

---

## 3. 实现方式

### 3.1 节点准备

打开 `giantWindmill.prefab`，确认扇叶是否是独立子物体。

推荐层级：

```text
giantWindmill
└── Body
└── BladePivot
    └── Blades
```

`WindmillBladeRotator` 挂在 `BladePivot` 上。

如果当前模型没有单独的扇叶轴节点：

1. 在扇叶中心创建空物体 `BladePivot`。
2. 将扇叶 Mesh 子物体拖到 `BladePivot` 下。
3. 调整 `BladePivot` 的位置到扇叶中心。
4. 让 `BladePivot` 的本地轴对齐扇叶旋转轴。

### 3.2 脚本逻辑

基础逻辑可以很小：

```csharp
using UnityEngine;

namespace Game.View.Environment
{
    public sealed class WindmillBladeRotator : MonoBehaviour
    {
        [SerializeField] private Vector3 localAxis = Vector3.forward;
        [SerializeField] private float rpm = 45f;
        [SerializeField] private bool randomStartAngle = true;

        private void Awake()
        {
            if (randomStartAngle)
            {
                transform.Rotate(localAxis.normalized, Random.Range(0f, 360f), Space.Self);
            }
        }

        private void Update()
        {
            float degrees = rpm * 6f * Time.deltaTime;
            transform.Rotate(localAxis.normalized, degrees, Space.Self);
        }
    }
}
```

说明：

- `rpm * 6` 是把每分钟转数转换成每秒角度：`360 / 60 = 6`。
- 如果转轴不对，不改代码，优先在 Inspector 里改 `localAxis`。
- 只做表现时不要放到 `Domain`，放在 `View` 层即可。

---

## 4. Unity 里怎么操作

1. 在 Project 窗口打开 `Assets/Resources/Prefab/Element/giantWindmill.prefab`。
2. 找到扇叶对应的 Transform；如果没有独立节点，按上面的方式建一个 `BladePivot`。
3. 把 `WindmillBladeRotator` 挂到 `BladePivot`。
4. 设置 `localAxis`，常见值：
   - 绕本地 Z 轴：`(0, 0, 1)`
   - 绕本地 X 轴：`(1, 0, 0)`
   - 绕本地 Y 轴：`(0, 1, 0)`
5. 设置 `rpm`，建议先用 `45`。
6. 进入 Play Mode 检查转轴是否正确。
7. 如果方向反了，把 `rpm` 改成负数，例如 `-45`。

---

## 5. 调参建议

| 场景 | `rpm` 建议 |
|---|---:|
| 装饰性慢转 | `10 ~ 25` |
| 普通有风 | `35 ~ 70` |
| 强风表现 | `80 ~ 120` |

注意点：

- 风车离镜头很远时，转速太快会闪烁，可以降低 `rpm`。
- 如果扇叶模型轴心偏了，优先修 Prefab 层级和 Pivot，不要用代码硬补位置。
- 如果项目后续接入风力等级，转速应由表现层读取风格子结果后平滑过渡。

---

## 6. 验收清单

- `giantWindmill` 放进场景后，Play Mode 下扇叶会转。
- Inspector 可以直接调速度、轴向和方向。
- 多个风车不会完全同步到同一个起始角度。
- 不产生运行时报错。
- 不修改风路规则、计分规则和配置表。

