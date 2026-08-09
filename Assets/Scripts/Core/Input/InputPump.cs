using UnityEngine;

namespace Glowpulse.Core.InputSystem
{
    /// <summary>
    /// Samples the active input provider once per frame, before any gameplay
    /// script runs. Everything else reads the cached values, so a frame is
    /// guaranteed to see a single consistent snapshot of the controls.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class InputPump : MonoBehaviour
    {
        private void Update()
        {
            // Unscaled so menus and slow-motion still feel responsive.
            InputService.Raw.Sample(Time.unscaledDeltaTime);
        }

        private void OnDisable()
        {
            InputService.Raw.Flush();
        }

        public static InputPump Install(GameObject host)
        {
            return host.GetComponent<InputPump>() ?? host.AddComponent<InputPump>();
        }
    }
}
