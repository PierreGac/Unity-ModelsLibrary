using System;
using System.Reflection;
using System.Threading;
using ModelLibrary.Editor.Infrastructure;
using NUnit.Framework;
using UnityEditor;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Guards the domain-reload hang where a custom synchronization context pumped from
    /// <see cref="EditorApplication.update"/> never returned to the editor.
    /// </summary>
    public class UnityMainThreadSyncContextTests
    {
        private const string PumpMethodName = "Pump";
        private const string UnitySynchronizationContextTypeName = "UnitySynchronizationContext";

        /// <summary>
        /// Domain load must leave Unity's synchronization context installed and must not
        /// subscribe a pump to <see cref="EditorApplication.update"/>.
        /// </summary>
        [Test]
        public void DomainLoad_LeavesUnitySynchronizationContextAndDoesNotPumpUpdate()
        {
            Assert.IsFalse(UnityMainThreadSyncContext.InstalledFallback);
            Assert.IsTrue(UnityMainThreadSyncContext.IsMainThread);

            SynchronizationContext current = SynchronizationContext.Current;
            Assert.IsNotNull(current, "Unity installs UnitySynchronizationContext before user code.");
            Assert.AreEqual(UnitySynchronizationContextTypeName, current.GetType().Name);

            EditorApplication.CallbackFunction update = EditorApplication.update;
            if (update == null)
            {
                return;
            }

            Delegate[] listeners = update.GetInvocationList();
            for (int i = 0; i < listeners.Length; i++)
            {
                MethodInfo method = listeners[i].Method;
                bool isPump = method != null
                    && method.Name == PumpMethodName
                    && method.DeclaringType == typeof(UnityMainThreadSyncContext);
                Assert.IsFalse(isPump, "Pump must not be subscribed to EditorApplication.update.");
            }
        }

        /// <summary>
        /// A main-thread post runs immediately and does not schedule editor update work.
        /// </summary>
        [Test]
        public void Post_OnMainThread_RunsInline()
        {
            bool ran = false;
            UnityMainThreadSyncContext.Post(() => ran = true);
            Assert.IsTrue(ran);
        }
    }
}
