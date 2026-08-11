using FloatingIsLand.GameInput;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// 在 PC 上验手机操作：打开后编辑器按触屏规则跑（HUD 出触屏工具条、摆放改成点两下确认），
    /// 同时启用 Input System 的触摸模拟，把鼠标当成一根手指。
    ///
    /// 一根手指验不了的只有双指手势（捏合缩放、双指转视角），那几条只能上真机。
    /// 其余全部——点选落点、二次确认、工具条按钮、UI 遮挡判定——在编辑器里就能走通。
    ///
    /// 开关存 EditorPrefs 而不是内存：点完菜单进 Play 会走一次域重载，静态字段全清零。
    /// </summary>
    public static class TouchDebugMenu
    {
        private const string MenuPath = "Tools/调试/触屏模拟（把鼠标当手指）";

        [MenuItem(MenuPath, priority = 300)]
        private static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(PointerInput.ForceTouchPrefKey, false);
            EditorPrefs.SetBool(PointerInput.ForceTouchPrefKey, next);
            PointerInput.ForceTouchMode = next;

            Debug.Log(next
                ? "[输入] 触屏模拟已开启：编辑器按手机规则跑（鼠标 = 一根手指）。双指手势验不了，那几条得上真机。"
                : "[输入] 触屏模拟已关闭，回到鼠标键盘操作。");
        }

        [MenuItem(MenuPath, validate = true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PointerInput.ForceTouchPrefKey, false));
            return true;
        }
    }
}
