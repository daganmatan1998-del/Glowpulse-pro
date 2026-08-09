using Glowpulse.CameraSystem;
using UnityEngine;
#if URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace Glowpulse.Core.Bootstrap
{
    /// <summary>Builds the gameplay camera and hooks it to the player.</summary>
    public static class CameraFactory
    {
        public static ThirdPersonCamera Create(Transform followTarget, CameraConfig config = null)
        {
            var go = new GameObject("Main Camera");
            go.SetActive(false);
            PlayerFactory.TrySetTag(go, "MainCamera");

            var camera = go.AddComponent<UnityEngine.Camera>();
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 420f;
            camera.fieldOfView = (config != null ? config : CameraConfig.Default).BaseFov;
            camera.allowHDR = true;
            camera.allowMSAA = true;

            go.AddComponent<AudioListener>();
            go.AddComponent<CameraShaker>();

            var rig = go.AddComponent<ThirdPersonCamera>();

#if URP_PRESENT
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.Medium;
            data.renderShadows = true;
            data.dithering = true;
#endif

            go.SetActive(true);

            rig.Target = followTarget;
            if (followTarget != null)
                rig.SnapTo(followTarget.eulerAngles.y, rig.Config.DefaultPitch);

            return rig;
        }
    }
}
