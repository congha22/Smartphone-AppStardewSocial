using System;
using System.Threading.Tasks;
using StardewModdingAPI;

namespace SmartphoneAppStardewSocial
{
    public partial class ModEntry
    {
        public static bool IsAiUsageLimitEnabled()
        {
            return IsSharedAiProviderMode();
        }

        public static async Task RunAiActionWithQueueAsync(Func<Task> action, string queueKey = "", bool highPriority = false)
        {
            if (action == null)
                return;

            await RunAiActionSafeAsync(action);
        }

        private static async Task RunAiActionSafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"AI action failed: {ex}", LogLevel.Trace);
            }
        }
    }
}
