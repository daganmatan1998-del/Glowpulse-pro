using Glowpulse.Core.Rendering;
using UnityEngine;

namespace Glowpulse.Core.Characters
{
    /// <summary>
    /// Builds an articulated humanoid out of primitives: a real joint hierarchy
    /// with hips, spine, limbs and hands rather than a single capsule. It costs
    /// about twenty renderers per character but gives the procedural animator
    /// something to actually animate, which is what sells the combat.
    ///
    /// When authored models arrive, this whole class is replaced by instantiating
    /// a prefab and mapping its bones into <see cref="CharacterRig"/> - nothing
    /// downstream of the rig needs to change.
    /// </summary>
    public static class CharacterRigFactory
    {
        /// <summary>
        /// Creates the visual hierarchy under <paramref name="owner"/> and returns
        /// the rig describing it. The owner keeps its own transform at the
        /// character's feet.
        /// </summary>
        public static CharacterRig Build(GameObject owner, in CharacterStyle style, bool castShadows = true)
        {
            float h = Mathf.Max(0.6f, style.Height);
            float build = Mathf.Max(0.5f, style.Build);

            Material skin = MaterialLibrary.Lit(style.Skin, 0.18f);
            Material torso = MaterialLibrary.Lit(style.Torso, 0.16f);
            Material legs = MaterialLibrary.Lit(style.Legs, 0.14f);
            Material shoes = MaterialLibrary.Lit(style.Shoes, 0.3f);
            Material hair = MaterialLibrary.Lit(style.Hair, 0.22f);
            Material accent = style.AccentGlow > 0.01f
                ? MaterialLibrary.Emissive(style.Accent, style.AccentGlow, 0.4f)
                : MaterialLibrary.Lit(style.Accent, 0.35f);

            var rig = owner.GetComponent<CharacterRig>() ?? owner.AddComponent<CharacterRig>();

            Transform root = NewJoint("Rig", owner.transform, Vector3.zero);
            rig.Set(RigBone.Root, root);

            // ---- proportions, all derived from total height -------------------
            float hipY = 0.525f * h;
            float thighLen = 0.245f * h;
            float shinLen = 0.235f * h;
            float ankleY = 0.045f * h;
            float hipHalfWidth = 0.085f * h * Mathf.Lerp(1f, 1.1f, build - 1f);
            float shoulderHalfWidth = 0.105f * h * build;
            float upperArmLen = 0.175f * h;
            float lowerArmLen = 0.16f * h;
            float limbR = 0.052f * h * build;

            // ---- spine --------------------------------------------------------
            Transform hips = NewJoint("Hips", root, new Vector3(0f, hipY, 0f));
            Transform spine = NewJoint("Spine", hips, new Vector3(0f, 0.075f * h, 0f));
            Transform chest = NewJoint("Chest", spine, new Vector3(0f, 0.105f * h, 0f));
            Transform neck = NewJoint("Neck", chest, new Vector3(0f, 0.105f * h, 0f));
            Transform head = NewJoint("Head", neck, new Vector3(0f, 0.055f * h, 0f));

            rig.Set(RigBone.Hips, hips);
            rig.Set(RigBone.Spine, spine);
            rig.Set(RigBone.Chest, chest);
            rig.Set(RigBone.Neck, neck);
            rig.Set(RigBone.Head, head);

            // pelvis + torso blocks
            MeshLibrary.CreatePart("Pelvis", hips, MeshLibrary.Cube, legs,
                new Vector3(0f, 0.02f * h, 0f), Quaternion.identity,
                new Vector3(hipHalfWidth * 2.1f, 0.1f * h, 0.13f * h * build), castShadows);

            MeshLibrary.CreatePart("Torso", chest, MeshLibrary.Cube, torso,
                new Vector3(0f, 0.005f * h, 0f), Quaternion.identity,
                new Vector3(shoulderHalfWidth * 1.95f, 0.235f * h, 0.135f * h * build), castShadows);

            // shoulder caps round off the boxy silhouette
            MeshLibrary.CreatePart("DeltoidL", chest, MeshLibrary.Sphere, torso,
                new Vector3(shoulderHalfWidth, 0.085f * h, 0f), Quaternion.identity,
                Vector3.one * (limbR * 2.5f), castShadows);
            MeshLibrary.CreatePart("DeltoidR", chest, MeshLibrary.Sphere, torso,
                new Vector3(-shoulderHalfWidth, 0.085f * h, 0f), Quaternion.identity,
                Vector3.one * (limbR * 2.5f), castShadows);

            if (style.Shirtless)
            {
                BuildBareTorso(chest, skin, h, build, shoulderHalfWidth, castShadows);
                BuildBackTattoo(chest, style, h, build, shoulderHalfWidth);
            }
            else
            {
                // accent stripe down the chest - also the enemy-type colour tell
                MeshLibrary.CreatePart("Accent", chest, MeshLibrary.Cube, accent,
                    new Vector3(0f, 0.02f * h, 0.07f * h * build), Quaternion.identity,
                    new Vector3(0.055f * h, 0.17f * h, 0.012f * h), false);
            }

            MeshLibrary.CreatePart("Belt", hips, MeshLibrary.Cube, accent,
                new Vector3(0f, 0.075f * h, 0f), Quaternion.identity,
                new Vector3(hipHalfWidth * 2.15f, 0.022f * h, 0.14f * h * build), false);

            // ---- head ---------------------------------------------------------
            MeshLibrary.CreatePart("Neck", neck, MeshLibrary.Capsule, skin,
                new Vector3(0f, 0.018f * h, 0f), Quaternion.identity,
                new Vector3(limbR * 1.15f, 0.032f * h, limbR * 1.15f), castShadows);

            MeshLibrary.CreatePart("Skull", head, MeshLibrary.Sphere, skin,
                new Vector3(0f, 0.055f * h, 0f), Quaternion.identity,
                new Vector3(0.115f * h, 0.135f * h, 0.125f * h), castShadows);

            MeshLibrary.CreatePart("Hair", head, MeshLibrary.Sphere, hair,
                new Vector3(0f, 0.068f * h, -0.008f * h), Quaternion.identity,
                new Vector3(0.121f * h, 0.115f * h, 0.128f * h), false);

            // ---- arms ---------------------------------------------------------
            BuildArm(rig, chest, +1f, shoulderHalfWidth, upperArmLen, lowerArmLen, limbR, h,
                skin, torso, accent, castShadows,
                RigBone.ShoulderL, RigBone.UpperArmL, RigBone.LowerArmL, RigBone.HandL, "L");
            BuildArm(rig, chest, -1f, shoulderHalfWidth, upperArmLen, lowerArmLen, limbR, h,
                skin, torso, accent, castShadows,
                RigBone.ShoulderR, RigBone.UpperArmR, RigBone.LowerArmR, RigBone.HandR, "R");

            // ---- legs ---------------------------------------------------------
            BuildLeg(rig, hips, +1f, hipHalfWidth, thighLen, shinLen, ankleY, limbR, h,
                legs, shoes, castShadows, RigBone.ThighL, RigBone.ShinL, RigBone.FootL, "L");
            BuildLeg(rig, hips, -1f, hipHalfWidth, thighLen, shinLen, ankleY, limbR, h,
                legs, shoes, castShadows, RigBone.ThighR, RigBone.ShinR, RigBone.FootR, "R");

            // A neutral fighting-game A-pose reads better than a stiff T-pose and
            // becomes the rest pose everything else is layered on top of.
            ApplyBaseStance(rig);
            rig.Configure(h, 0.72f * h);
            rig.CaptureRestPose();
            return rig;
        }

        /// <summary>
        /// Muscle masses over a bare chest: pectorals, abdominals, obliques and a
        /// trapezius wedge into the neck.
        ///
        /// Without these a shirtless character is a flesh-coloured box, because
        /// the shirt was doing all the work of breaking up the torso. They are
        /// slightly proud of the torso block so they catch light along their own
        /// edges, which is what reads as bulk at gameplay distance.
        /// </summary>
        private static void BuildBareTorso(Transform chest, Material skin, float h, float build,
            float shoulderHalfWidth, bool shadows)
        {
            float depth = 0.135f * h * build;
            float front = depth * 0.5f;

            // Pectorals: two flattened spheres, spread with the shoulders.
            for (int side = -1; side <= 1; side += 2)
            {
                MeshLibrary.CreatePart(side > 0 ? "PecL" : "PecR", chest, MeshLibrary.Sphere, skin,
                    new Vector3(side * shoulderHalfWidth * 0.52f, 0.062f * h, front * 0.62f),
                    Quaternion.identity,
                    new Vector3(0.105f * h * build, 0.062f * h, 0.075f * h * build), shadows);
            }

            // Abdominals: three tapering blocks, narrowing towards the belt so the
            // torso reads as a V rather than a slab.
            for (int i = 0; i < 3; i++)
            {
                float t = i / 2f;
                float width = Mathf.Lerp(0.155f, 0.115f, t) * h * build;
                MeshLibrary.CreatePart("Abs" + i, chest, MeshLibrary.Cube, skin,
                    new Vector3(0f, -0.012f * h - i * 0.042f * h, front * 0.72f),
                    Quaternion.identity,
                    new Vector3(width, 0.034f * h, 0.03f * h), shadows);
            }

            // Obliques flare the ribcage out above the waist.
            for (int side = -1; side <= 1; side += 2)
            {
                MeshLibrary.CreatePart(side > 0 ? "ObliqueL" : "ObliqueR", chest,
                    MeshLibrary.Sphere, skin,
                    new Vector3(side * shoulderHalfWidth * 0.86f, -0.01f * h, 0f),
                    Quaternion.identity,
                    new Vector3(0.05f * h * build, 0.115f * h, 0.1f * h * build), shadows);
            }

            // Traps, which is most of what makes a heavy build read from behind.
            MeshLibrary.CreatePart("Traps", chest, MeshLibrary.Sphere, skin,
                new Vector3(0f, 0.105f * h, -0.012f * h), Quaternion.identity,
                new Vector3(shoulderHalfWidth * 1.5f, 0.05f * h, 0.09f * h * build), shadows);
        }

        /// <summary>
        /// A back piece, built from flat slabs laid just proud of the spine: a
        /// central bar with wings sweeping up and out across the shoulder blades.
        ///
        /// Drawn as geometry rather than a texture because the whole project has
        /// no authored art - and because it survives being replaced later by a
        /// real decal on a real model without any gameplay code changing.
        /// </summary>
        private static void BuildBackTattoo(Transform chest, in CharacterStyle style, float h,
            float build, float shoulderHalfWidth)
        {
            if (style.Tattoo.a <= 0.01f) return;

            Material ink = MaterialLibrary.Lit(style.Tattoo, 0.05f);
            float back = -0.135f * h * build * 0.5f - 0.004f * h;
            float thickness = 0.01f * h;

            // Spine bar, running most of the height of the back.
            MeshLibrary.CreatePart("TattooSpine", chest, MeshLibrary.Cube, ink,
                new Vector3(0f, 0.012f * h, back), Quaternion.identity,
                new Vector3(0.034f * h, 0.215f * h, thickness), false);

            // Five feathers per side, fanning out across the shoulder blades. The
            // fan starts below the centre line so the design covers the back
            // rather than perching on top of it.
            //
            // Every width is a fraction of the shoulder half-width rather than of
            // height, so the ink stays on the skin. Measured against height it
            // overhangs the silhouette on a heavy build and the tattoo appears to
            // float in the air beside the character.
            float inner = shoulderHalfWidth * 0.1f;

            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    float t = i / 4f;
                    float length = Mathf.Lerp(0.82f, 0.44f, t) * shoulderHalfWidth;
                    float rise = Mathf.Lerp(-0.022f, 0.088f, t) * h;
                    float tilt = Mathf.Lerp(6f, 46f, t) * side;

                    // The bar is centred, so its centre sits half its own length
                    // out from the spine. Getting this wrong bunches the whole
                    // fan into a smudge over the shoulders.
                    float offset = side * (inner + length * 0.5f);

                    MeshLibrary.CreatePart($"TattooWing{(side > 0 ? "L" : "R")}{i}", chest,
                        MeshLibrary.Cube, ink,
                        new Vector3(offset, rise, back),
                        Quaternion.Euler(0f, 0f, tilt),
                        new Vector3(length, 0.019f * h, thickness), false);
                }
            }

            // A band low on the back closes the design off above the belt.
            MeshLibrary.CreatePart("TattooBand", chest, MeshLibrary.Cube, ink,
                new Vector3(0f, -0.098f * h, back), Quaternion.identity,
                new Vector3(shoulderHalfWidth * 1.3f, 0.016f * h, thickness), false);
        }

        private static void BuildArm(CharacterRig rig, Transform chest, float side,
            float shoulderHalfWidth, float upperLen, float lowerLen, float limbR, float h,
            Material skin, Material sleeve, Material accent, bool shadows,
            RigBone shoulderBone, RigBone upperBone, RigBone lowerBone, RigBone handBone, string suffix)
        {
            Transform shoulder = NewJoint("Shoulder" + suffix, chest,
                new Vector3(side * shoulderHalfWidth, 0.085f * h, 0f));
            Transform upper = NewJoint("UpperArm" + suffix, shoulder, Vector3.zero);
            Transform lower = NewJoint("LowerArm" + suffix, upper, new Vector3(0f, -upperLen, 0f));
            Transform hand = NewJoint("Hand" + suffix, lower, new Vector3(0f, -lowerLen, 0f));

            rig.Set(shoulderBone, shoulder);
            rig.Set(upperBone, upper);
            rig.Set(lowerBone, lower);
            rig.Set(handBone, hand);

            MeshLibrary.CreatePart("UpperArmMesh", upper, MeshLibrary.Capsule, sleeve,
                new Vector3(0f, -upperLen * 0.5f, 0f), Quaternion.identity,
                new Vector3(limbR * 1.7f, upperLen * 0.56f, limbR * 1.7f), shadows);

            MeshLibrary.CreatePart("LowerArmMesh", lower, MeshLibrary.Capsule, skin,
                new Vector3(0f, -lowerLen * 0.5f, 0f), Quaternion.identity,
                new Vector3(limbR * 1.45f, lowerLen * 0.56f, limbR * 1.45f), shadows);

            MeshLibrary.CreatePart("Fist", hand, MeshLibrary.Sphere, accent,
                new Vector3(0f, -limbR * 0.9f, 0f), Quaternion.identity,
                Vector3.one * (limbR * 2.05f), shadows);
        }

        private static void BuildLeg(CharacterRig rig, Transform hips, float side,
            float hipHalfWidth, float thighLen, float shinLen, float ankleY, float limbR, float h,
            Material trousers, Material shoes, bool shadows,
            RigBone thighBone, RigBone shinBone, RigBone footBone, string suffix)
        {
            Transform thigh = NewJoint("Thigh" + suffix, hips, new Vector3(side * hipHalfWidth, -0.02f * h, 0f));
            Transform shin = NewJoint("Shin" + suffix, thigh, new Vector3(0f, -thighLen, 0f));
            Transform foot = NewJoint("Foot" + suffix, shin, new Vector3(0f, -shinLen, 0f));

            rig.Set(thighBone, thigh);
            rig.Set(shinBone, shin);
            rig.Set(footBone, foot);

            MeshLibrary.CreatePart("ThighMesh", thigh, MeshLibrary.Capsule, trousers,
                new Vector3(0f, -thighLen * 0.5f, 0f), Quaternion.identity,
                new Vector3(limbR * 2.05f, thighLen * 0.58f, limbR * 2.05f), shadows);

            MeshLibrary.CreatePart("ShinMesh", shin, MeshLibrary.Capsule, trousers,
                new Vector3(0f, -shinLen * 0.5f, 0f), Quaternion.identity,
                new Vector3(limbR * 1.75f, shinLen * 0.56f, limbR * 1.75f), shadows);

            MeshLibrary.CreatePart("Shoe", foot, MeshLibrary.Cube, shoes,
                new Vector3(0f, ankleY * 0.5f - 0.005f * h, 0.035f * h), Quaternion.identity,
                new Vector3(limbR * 2.1f, ankleY, 0.135f * h), shadows);
        }

        /// <summary>Slight A-pose with softly bent elbows and knees.</summary>
        private static void ApplyBaseStance(CharacterRig rig)
        {
            SetLocalEuler(rig, RigBone.UpperArmL, new Vector3(-6f, 0f, -7f));
            SetLocalEuler(rig, RigBone.UpperArmR, new Vector3(-6f, 0f, 7f));
            SetLocalEuler(rig, RigBone.LowerArmL, new Vector3(-14f, 0f, -3f));
            SetLocalEuler(rig, RigBone.LowerArmR, new Vector3(-14f, 0f, 3f));
            SetLocalEuler(rig, RigBone.ThighL, new Vector3(-1.5f, 0f, 1.5f));
            SetLocalEuler(rig, RigBone.ThighR, new Vector3(-1.5f, 0f, -1.5f));
            SetLocalEuler(rig, RigBone.ShinL, new Vector3(3f, 0f, 0f));
            SetLocalEuler(rig, RigBone.ShinR, new Vector3(3f, 0f, 0f));
        }

        private static void SetLocalEuler(CharacterRig rig, RigBone bone, Vector3 euler)
        {
            Transform t = rig.Get(bone);
            if (t != null) t.localRotation = Quaternion.Euler(euler);
        }

        private static Transform NewJoint(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }
    }
}
