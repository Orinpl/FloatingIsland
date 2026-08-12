# 风帆受风抖动需求拆分与使用说明

> 对应需求："还有风帆被风吹动的时候的抖动之类的"。
>
> 目标是让风帆在有风时出现轻微摆动、鼓起、抖动的表现。该需求只属于表现层，不改变风帆转向风路的领域规则。

---

## 1. 适用资源

当前项目里已有风帆建筑 Prefab：

- `Assets/Resources/Prefab/Building/sail_01.prefab`

推荐新增脚本目录：

- `Assets/Script/View/Environment/SailWindShake.cs`

如果以后风帆有多个型号，可以继续使用同一个组件，只针对不同 Prefab 调参数。

---

## 2. 需求拆分

### 2.1 基础版本：Transform 抖动

适合快速做出效果，成本最低。

实现方式：

- 找到风帆布面的子节点，例如 `SailCloth`。
- 给该节点或它的父节点挂 `SailWindShake`。
- 每帧根据 `sin` 和噪声做轻微本地旋转、位移或缩放。
- 风越强，摆动幅度越大；无风时只保留很弱的待机摆动或停止。

优点：

- 实现快。
- 不要求 Mesh 可读。
- 不需要新 Shader。

缺点：

- 整块布一起动，近看不够像布料。

### 2.2 进阶版本：顶点波动

适合镜头能近距离看到风帆时使用。

实现方式：

- 在运行时复制风帆 Mesh。
- 缓存原始顶点。
- 根据顶点高度或 UV，越靠近固定边摆动越小，越靠近自由边摆动越大。
- 沿布面法线或横向做波形偏移。
- 风力越高，幅度和频率越高。

优点：

- 近看更像被风吹动的布。
- 可以做出从固定边到自由边逐渐变大的抖动。

缺点：

- 需要 Mesh 可读，或需要在导入设置里开启 Read/Write。
- 顶点很多时每帧改 Mesh 有一定开销。

---

## 3. 推荐实现顺序

先做基础版本，再按需要升级。

1. 先实现 Transform 抖动，确认风帆在 Play Mode 下有动态。
2. 再接入风力等级，让无风、小风、大风有不同幅度。
3. 如果近景效果不够，再把 `SailWindShake` 扩展成顶点波动模式。
4. 最后再考虑 Shader 风动，不建议一开始就做。

---

## 4. 推荐参数

| 参数 | 建议默认值 | 说明 |
|---|---:|---|
| `windStrength` | `1` | 表现层使用的风力强度，后续可接领域层 `ResultForce` |
| `positionAmplitude` | `0.03` | 本地位移幅度 |
| `rotationAmplitude` | `3` | 本地旋转幅度，单位角度 |
| `frequency` | `1.5` | 摆动频率 |
| `flutterFrequency` | `8` | 高频细抖频率 |
| `flutterAmount` | `0.25` | 高频细抖占比 |
| `windDirection` | `(1, 0, 0)` | 世界风向或本地风向，按接入方式定 |

调参原则：

- 远景风帆：位移小、旋转稍明显。
- 近景风帆：旋转小、顶点波动明显。
- 建造游戏里不要抖得太夸张，否则会干扰玩家看格子和建筑朝向。

---

## 5. Unity 里怎么操作

1. 打开 `Assets/Resources/Prefab/Building/sail_01.prefab`。
2. 找到风帆布面节点；建议命名为 `SailCloth`。
3. 把 `SailWindShake` 挂到 `SailCloth`，不要挂到整个建筑根节点，避免底座也跟着抖。
4. 初始用基础参数：

```text
positionAmplitude = 0.02
rotationAmplitude = 2
frequency = 1.2
flutterFrequency = 7
flutterAmount = 0.2
```

5. 进入 Play Mode，观察风帆是否只在布面上产生轻微动态。
6. 如果整块建筑跟着动，说明组件挂错节点，需要挪到布面子节点。
7. 如果抖动方向不对，调整 `windDirection` 或节点本地轴向。
8. 如果要接风系统，表现层读取当前风帆所在格子的 `ResultForce`，把它映射到 `windStrength`。

---

## 6. 脚本逻辑示例

基础 Transform 版本可以这样写：

```csharp
using UnityEngine;

namespace Game.View.Environment
{
    public sealed class SailWindShake : MonoBehaviour
    {
        [SerializeField] private float windStrength = 1f;
        [SerializeField] private float positionAmplitude = 0.02f;
        [SerializeField] private float rotationAmplitude = 2f;
        [SerializeField] private float frequency = 1.2f;
        [SerializeField] private float flutterFrequency = 7f;
        [SerializeField] private float flutterAmount = 0.2f;
        [SerializeField] private Vector3 localMoveAxis = Vector3.right;
        [SerializeField] private Vector3 localRotateAxis = Vector3.forward;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float phase;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            phase = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float t = Time.time + phase;
            float slow = Mathf.Sin(t * frequency);
            float flutter = Mathf.Sin(t * flutterFrequency) * flutterAmount;
            float wave = (slow + flutter) * Mathf.Max(0f, windStrength);

            transform.localPosition = baseLocalPosition + localMoveAxis.normalized * (wave * positionAmplitude);
            transform.localRotation = baseLocalRotation * Quaternion.AngleAxis(wave * rotationAmplitude, localRotateAxis.normalized);
        }
    }
}
```

后续接入领域风力时，不建议让这个组件自己计算格子风；应该由表现层控制器把风力传进来，例如：

```csharp
public void SetWindStrength(float value)
{
    windStrength = Mathf.Max(0f, value);
}
```

---

## 7. 顶点波动升级方案

当 Transform 抖动不够时，再升级到 Mesh 形变。

操作条件：

- 风帆 Mesh 需要开启 `Read/Write`。
- `SailWindShake` 需要挂在带 `MeshFilter` 的布面节点上。
- 运行时复制 Mesh，避免直接改 Project 里的原始资源。

推荐形变规则：

- 以顶点本地高度或 UV.y 作为权重。
- 固定边权重接近 `0`，自由边权重接近 `1`。
- 偏移方向使用布面的本地法线或横向轴。
- 每帧结束后调用 `mesh.RecalculateNormals()`，如果视觉上不需要法线变化，可跳过以省性能。

验收重点：

- 固定边不能明显脱离杆子。
- 自由边有波纹。
- 多个风帆相位不同。
- 停用组件后可以恢复原始 Mesh。

---

## 8. 验收清单

- `sail_01` 放进场景后，Play Mode 下只有风帆布面在动。
- 无风时动作很轻或停止，有风时抖动增强。
- 多个风帆不会完全同步。
- 抖动不影响建筑根节点位置、占格、旋转和点击。
- 不修改风路规则、计分规则和配置表。

