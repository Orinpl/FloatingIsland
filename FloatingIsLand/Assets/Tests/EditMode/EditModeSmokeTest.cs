using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>M0 冒烟测试：验证 EditMode 测试链路可用（Window → General → Test Runner 里应看到本条并通过）。</summary>
    public sealed class EditModeSmokeTest
    {
        [Test]
        public void TestRunnerWorks()
        {
            Assert.Pass("EditMode 测试链路正常。");
        }
    }
}
