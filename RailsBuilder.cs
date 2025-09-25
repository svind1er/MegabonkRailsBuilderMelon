using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Actors.Player;
using Il2CppAssets.Scripts.Utility;
using MelonLoader;
using UnityEngine;

namespace MegaRailsBuilder
{
    public class RailsBuilder : MelonMod
    {
        private GameObject ghostRail;
        private int currentRailIndex;
        private bool buildMode;
        private float rotationY;
        private bool flipped;
        private SpawnInteractables spawner;
        private static Material ghostMaterial;
        private static Texture2D bgTex;

        public static bool freezeToggle = false;
        public static float cachedRunTimer;
        public static float cachedStageTimer;

        public override void OnUpdate()
        {
            if (Input.GetKeyUp(KeyCode.F1))
            {
                buildMode = !buildMode;
                if (!buildMode && ghostRail != null)
                {
                    UnityEngine.Object.Destroy(ghostRail);
                    ghostRail = null;
                    rotationY = 0f;
                    flipped = false;
                }
#if DEBUG
                MelonLogger.Msg(buildMode ? "[!] Build Mode ON" : "[!] Build Mode OFF");
#endif
            }

            if (buildMode && Input.GetKeyDown(KeyCode.F2))
            {
                freezeToggle = !freezeToggle;
#if DEBUG
                MelonLogger.Msg(freezeToggle ? "[!] Freeze ENABLED (Game time paused)." : "[!] Freeze DISABLED (Game time resumed).");
#endif
            }

            if (!buildMode) return;

            if (spawner == null) spawner = UnityEngine.Object.FindObjectOfType<SpawnInteractables>();
            if (spawner == null || spawner.rails == null || spawner.rails.Length == 0) return;

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                currentRailIndex = (currentRailIndex - 1 + spawner.rails.Length) % spawner.rails.Length;
                RefreshGhost(spawner.rails[currentRailIndex]);
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                currentRailIndex = (currentRailIndex + 1) % spawner.rails.Length;
                RefreshGhost(spawner.rails[currentRailIndex]);
            }

            if (Input.GetKey(KeyCode.Q)) rotationY -= 90f * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) rotationY += 90f * Time.deltaTime;
            if (Input.GetKeyUp(KeyCode.Z)) rotationY += 90f;
            if (Input.GetKeyUp(KeyCode.X)) rotationY -= 90f;
            if (Input.GetKeyUp(KeyCode.C)) rotationY = 0;
            if (Input.GetKeyUp(KeyCode.F)) flipped = !flipped;

            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            Camera cam = Camera.main;

            if (cam != null)
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                Vector3 origin = ray.origin;
                Vector3 dir = ray.direction;
                float dist = 0f;

                while (dist < 200f)
                {
                    if (Physics.Raycast(origin, dir, out RaycastHit hit, 200f - dist))
                    {
                        dist += hit.distance + 0.01f;
                        origin = hit.point + dir * 0.01f;

                        if (hit.collider != null && hit.collider.GetComponentInParent<MyPlayer>() != null)
                            continue;

                        pos = hit.point;
                        rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0, rotationY, 0);
                        break;
                    }
                    else break;
                }
            }

            if (ghostRail == null)
                RefreshGhost(spawner.rails[currentRailIndex]);

            if (ghostRail != null)
            {
                ghostRail.transform.SetPositionAndRotation(pos, rot);

                Vector3 scale = ghostRail.transform.localScale;
                scale.x = flipped ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                ghostRail.transform.localScale = scale;

                if (Input.GetMouseButtonDown(0) && !Cursor.visible)
                {
                    GameObject placed = UnityEngine.Object.Instantiate(spawner.rails[currentRailIndex], pos, rot);

                    Vector3 pscale = placed.transform.localScale;
                    pscale.x = flipped ? -Mathf.Abs(pscale.x) : Mathf.Abs(pscale.x);
                    placed.transform.localScale = pscale;
#if DEBUG
                    MelonLogger.Msg($"[!] Placed rail {currentRailIndex} at {pos}");
#endif
                }
            }
        }

        public override void OnGUI()
        {
            if (!buildMode) return;

            float width = 350f;
            float height = 230f;
            float x = 350f;
            float y = 500f;

            if (bgTex == null)
            {
                bgTex = new Texture2D(1, 1);
                bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
                bgTex.Apply();
            }
            GUI.DrawTexture(new Rect(x, y, width, height), bgTex);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.cyan },
                fontStyle = FontStyle.Bold
            };

            GUIStyle railStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.yellow },
                fontStyle = FontStyle.Bold
            };

            GUIStyle instrStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.LowerCenter,
                normal = { textColor = Color.white },
                fontStyle = FontStyle.Bold
            };

            GUIStyle freezeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold,
                richText = true
            };

            GUI.Label(new Rect(x, y + 5, width, 25), "BUILD MODE", titleStyle);

            string railName = (spawner != null && spawner.rails.Length > 0) ? spawner.rails[currentRailIndex].name : "N/A";
            GUI.Label(new Rect(x, y + 35, width, 24), $"Selected Rail: {railName}", railStyle);

            string freezeText = $"<color=white>Frozen:</color> " + (freezeToggle ? "<color=green>ON</color>" : "<color=red>OFF</color>");
            Rect freezeRect = new Rect(x, y + 55, width, 28);
            GUI.Label(freezeRect, freezeText, freezeStyle);

            string[] lines =
            {
                "← / → : Switch Rails",
                "Q / E : Rotate Rail",
                "Z / X : Rotate by 90*",
                "C : Reset rotation",
                "F : Flip",
                "LMB : Place Rail",
                "F1 : Toggle Build Mode",
                "F2 : Toggle Freeze"
            };

            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(x + 10, y + 80 + (i * 18), width - 20, 20), lines[i], instrStyle);
        }

        private void RefreshGhost(GameObject prefab)
        {
            if (ghostRail != null) UnityEngine.Object.Destroy(ghostRail);
            if (prefab == null) return;

            ghostRail = UnityEngine.Object.Instantiate(prefab);
            ghostRail.name = "GhostRail";

            if (ghostMaterial == null)
            {
                ghostMaterial = new Material(Shader.Find("Standard"))
                {
                    color = new Color(1f, 1f, 1f, 0.3f),
                    renderQueue = 3000
                };
                ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghostMaterial.SetInt("_ZWrite", 0);
                ghostMaterial.EnableKeyword("_ALPHABLEND_ON");
            }

            ApplyGhost(ghostRail.transform);
#if DEBUG
            MelonLogger.Msg($"[!] Created ghost for prefab: {prefab.name}");
#endif
        }

        private void ApplyGhost(Transform t)
        {
            Collider col = t.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Renderer r = t.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = ghostMaterial;

            for (int i = 0; i < t.childCount; i++)
                ApplyGhost(t.GetChild(i));
        }
    }

    [HarmonyPatch]
    internal static class EnemySpawnBlockPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var enemyMgrType = typeof(Il2CppAssets.Scripts.Managers.EnemyManager);
            return AccessTools.GetDeclaredMethods(enemyMgrType).Where(m => m.Name == "SpawnEnemy");
        }

        private static bool Prefix()
        {
            if (RailsBuilder.freezeToggle)
            {
#if DEBUG
                MelonLogger.Msg("[!] Blocked an enemy spawn.");
#endif
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MyTime), "Update")]
    public static class FreezeTimerPatch
    {
        private static bool Prefix()
        {
            if (RailsBuilder.freezeToggle)
            {
                MyTime.runTimer = RailsBuilder.cachedRunTimer;
                MyTime.stageTimer = RailsBuilder.cachedStageTimer;
                return false;
            }
            else
            {
                RailsBuilder.cachedRunTimer = MyTime.runTimer;
                RailsBuilder.cachedStageTimer = MyTime.stageTimer;
                return true;
            }
        }
    }
}