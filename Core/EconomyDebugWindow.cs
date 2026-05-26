using System;
using System.Reflection;
using UnityEngine;

namespace EconomyRevamp
{
    internal sealed class EconomyDebugWindow : MonoBehaviour
    {
        private static readonly MethodInfo EconCycleMethod =
            typeof(IslandMarket).GetMethod("EconCycle", BindingFlags.Instance | BindingFlags.NonPublic);

        private Rect _windowRect = new Rect(80f, 80f, 320f, 190f);
        private bool _visible;
        private string _lastAction = "No manual cycles run.";
        private Vector2 _scroll;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6))
            {
                _visible = !_visible;
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            _windowRect = GUI.Window(771177, _windowRect, DrawWindow, "Economy Debug");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Label("Run IslandMarket economy cycles");
            GUILayout.Label("Uses the same private EconCycle method as startup preheat.");

            GUILayout.BeginHorizontal();
            DrawCycleButton(1);
            DrawCycleButton(10);
            DrawCycleButton(50);
            DrawCycleButton(100);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(62f));
            GUILayout.Label(_lastAction);
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close", GUILayout.Width(80f)))
            {
                _visible = false;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private void DrawCycleButton(int count)
        {
            if (GUILayout.Button(count.ToString(), GUILayout.Height(32f)))
            {
                RunCycles(count);
            }
        }

        private void RunCycles(int count)
        {
            if (EconCycleMethod == null)
            {
                _lastAction = "Could not find IslandMarket.EconCycle via reflection.";
                return;
            }

            IslandMarket[] markets = FindObjectsOfType<IslandMarket>();
            int invocationCount = 0;
            DateTime started = DateTime.Now;

            try
            {
                for (int cycle = 0; cycle < count; cycle++)
                {
                    for (int i = 0; i < markets.Length; i++)
                    {
                        EconCycleMethod.Invoke(markets[i], null);
                        invocationCount++;
                    }
                }

                if (Plugin.Instance != null)
                {
                    Plugin.Instance.RequestSnapshotRefresh();
                }

                _lastAction = "Ran " + count + " cycles across " + markets.Length + " markets (" + invocationCount + " calls) at " + started.ToLongTimeString() + ".";
            }
            catch (Exception ex)
            {
                _lastAction = "Cycle run failed: " + ex.GetBaseException().Message;
            }
        }
    }
}
