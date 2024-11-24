using UnityEngine;
using UnityEditor;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

public class DeviceSimulatorInfoOverlay : EditorWindow
{
    private string outputText = "";
    private string previousDeviceInfo = "";
    private const float updateInterval = 0.5f;
    private double lastUpdateTime;
    private bool initializedSuccessfully = false;
    private const int maxInitAttempts = 10;
    private int initAttempts = 0;
    private Vector2 scrollPosition;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle smallLabelStyle;
    private GUIStyle toastStyle;
    private const float LABEL_WIDTH = 100f;
    private double copyFeedbackTime;
    private string lastCopiedValue = "";
    private double toastEndTime;
    private string toastMessage = "";
    private Dictionary<string, string> safeAreaValues = new Dictionary<string, string>();

    [MenuItem("Window/Device Resolution")]
    public static void ShowWindow()
    {
        GetWindow<DeviceSimulatorInfoOverlay>("Device Info");
    }

    void InitializeStyles()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontStyle = FontStyle.Normal;
            labelStyle.padding = new RectOffset(8, 8, 2, 2);
        }

        if (valueStyle == null)
        {
            valueStyle = new GUIStyle(EditorStyles.textField);
            valueStyle.fontStyle = FontStyle.Normal;
            valueStyle.padding = new RectOffset(8, 8, 2, 2);
            valueStyle.fixedHeight = EditorGUIUtility.singleLineHeight;
            valueStyle.clipping = TextClipping.Clip;
        }

        if (smallLabelStyle == null)
        {
            smallLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            smallLabelStyle.padding = new RectOffset(2, 2, 1, 1);
            smallLabelStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
        }

        if (toastStyle == null)
        {
            toastStyle = new GUIStyle(EditorStyles.helpBox);
            toastStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            toastStyle.fontSize = 11;
            toastStyle.padding = new RectOffset(8, 8, 4, 4);
            toastStyle.alignment = TextAnchor.MiddleCenter;
        }
    }

    private float GetContentWidth(string text)
    {
        return valueStyle.CalcSize(new GUIContent(text)).x + 10f; // Add padding
    }

    private void ShowCopyFeedback(string value, string type = "Value")
    {
        lastCopiedValue = value;
        copyFeedbackTime = EditorApplication.timeSinceStartup + 1;
        ShowToast($"{type} copied: {value}");
        Repaint();
    }

    private void DrawClickableValue(string value, float width)
    {
        Rect valueRect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, valueStyle);
        
        // Handle flash effect
        if (EditorApplication.timeSinceStartup < copyFeedbackTime && value == lastCopiedValue)
        {
            float alpha = (float)(copyFeedbackTime - EditorApplication.timeSinceStartup);
            EditorGUI.DrawRect(valueRect, new Color(0.2f, 0.5f, 1f, alpha * 0.3f));
        }
        
        if (GUI.Button(valueRect, value, valueStyle))
        {
            EditorGUIUtility.systemCopyBuffer = value;
            ShowCopyFeedback(value, "Value");
        }
    }

    private void DrawValueWithParts(string value)
    {
        if (value.Contains(" x "))  // Resolution format
        {
            var parts = value.Split(new[] { " x " }, System.StringSplitOptions.None);
            float width1 = GetContentWidth(parts[0]);
            float width2 = GetContentWidth(parts[1]);
            
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width1 + width2 + 15));
            DrawClickableValue(parts[0], width1);
            GUILayout.Label("x", smallLabelStyle, GUILayout.Width(15));
            DrawClickableValue(parts[1], width2);
            EditorGUILayout.EndHorizontal();
        }
        else if (value.StartsWith("(") && value.EndsWith(")"))  // Safe Area format
        {
            var cleanValue = value.Trim('(', ')');
            var parts = cleanValue.Split(',');
            
            EditorGUILayout.BeginVertical();
            for (int i = 0; i < parts.Length; i++)
            {
                var trimmedPart = parts[i].Trim();
                string label = i switch
                {
                    0 => "x",
                    1 => "y",
                    2 => "w",
                    3 => "h",
                    _ => i.ToString()
                };
                
                // Extract just the numerical value
                var numericValue = System.Text.RegularExpressions.Regex.Match(trimmedPart, @"[-+]?\d*\.?\d+").Value;
                float width = GetContentWidth(numericValue);
                
                EditorGUILayout.BeginHorizontal(GUILayout.Width(width + 30));
                GUILayout.Label(label + ":", smallLabelStyle, GUILayout.Width(25));
                DrawClickableValue(numericValue, width);
                EditorGUILayout.EndHorizontal();
                
                if (i < parts.Length - 1)
                {
                    GUILayout.Space(2);
                }
            }
            EditorGUILayout.EndVertical();
        }
        else  // Default format
        {
            float width = GetContentWidth(value);
            EditorGUILayout.BeginHorizontal(GUILayout.Width(width));
            DrawClickableValue(value, width);
            EditorGUILayout.EndHorizontal();
        }
    }

    private void ShowToast(string message)
    {
        toastMessage = message;
        toastEndTime = (float)EditorApplication.timeSinceStartup + 1.5f;
        Repaint();
    }

    private void DrawToast()
    {
        if (EditorApplication.timeSinceStartup < toastEndTime)
        {
            float alpha = (float)(toastEndTime - EditorApplication.timeSinceStartup) / 1.5f;
            var originalColor = GUI.color;
            GUI.color = new Color(1, 1, 1, alpha);
            
            var content = new GUIContent(toastMessage);
            GUI.Label(new Rect(5, 5, position.width - 10, 20), content, toastStyle);
            GUI.color = originalColor;
            Repaint();
        }
    }

    void OnGUI()
    {
        InitializeStyles();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Controls
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        if (GUILayout.Button("Copy All", EditorStyles.toolbarButton, GUILayout.Width(100)))
        {
            EditorGUIUtility.systemCopyBuffer = outputText;
            ShowToast("All Copied!");
        }
        
        GUILayout.FlexibleSpace();
        
        // Show copy feedback in toolbar
        if (EditorApplication.timeSinceStartup < copyFeedbackTime)
        {
            GUILayout.Label("Copied!", EditorStyles.toolbarButton);
        }
        
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Device Info
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (outputText.Contains("No simulator main") || outputText.Contains("Open Device Simulator"))
        {
            GUILayout.Label(outputText, labelStyle);
        }
        else
        {
            var lines = outputText.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line)) continue;
                
                var parts = line.Split(": ", 2, System.StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    EditorGUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button(parts[0] + ":", labelStyle, GUILayout.Width(LABEL_WIDTH)))
                    {
                        // Find all values with this label
                        var allValues = outputText.Split('\n')
                            .Where(l => !string.IsNullOrEmpty(l) && l.StartsWith(parts[0] + ":"))
                            .Select(l => l.Split(": ", 2))
                            .Where(p => p.Length == 2)
                            .Select(p => p[1])
                            .FirstOrDefault();
                        if (allValues != null)
                        {
                            EditorGUIUtility.systemCopyBuffer = allValues;
                            ShowCopyFeedback(allValues, parts[0]);
                        }
                    }
                    DrawValueWithParts(parts[1]);
                    EditorGUILayout.EndHorizontal();
                    GUILayout.Space(2); // Add spacing between rows
                }
                else
                {
                    GUILayout.Label(line, labelStyle);
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        
        DrawToast();  // Draw toast message last so it appears on top
    }

    void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.delayCall += () => {
            RefreshInfo();
            initializedSuccessfully = !outputText.Contains("No simulator main");
        };
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (!initializedSuccessfully && initAttempts < maxInitAttempts)
        {
            initAttempts++;
            RefreshInfo();
            initializedSuccessfully = !outputText.Contains("No simulator main");
            if (initializedSuccessfully)
            {
                initAttempts = 0;
            }
            Repaint();
            return;
        }

        if (EditorApplication.timeSinceStartup - lastUpdateTime < updateInterval)
            return;

        var simulatorWindow = Resources.FindObjectsOfTypeAll<EditorWindow>()
            .FirstOrDefault(w => w.GetType().Name == "SimulatorWindow");

        if (simulatorWindow != null)
        {
            var main = simulatorWindow.GetType().GetProperty("main").GetValue(simulatorWindow);
            if (main != null)
            {
                var screenSim = main.GetType().GetProperty("ScreenSimulation").GetValue(main);
                if (screenSim != null)
                {
                    var currentDeviceInfo = screenSim.GetType().GetField("m_DeviceInfo", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(screenSim)?.ToString();
                    if (currentDeviceInfo != previousDeviceInfo)
                    {
                        RefreshInfo();
                        previousDeviceInfo = currentDeviceInfo;
                        Repaint(); // Force the window to update
                    }
                }
            }
        }

        lastUpdateTime = EditorApplication.timeSinceStartup;
    }

    private void RefreshInfo()
    {
        var simulatorWindow = Resources.FindObjectsOfTypeAll<EditorWindow>()
            .FirstOrDefault(w => w.GetType().Name == "SimulatorWindow");

        if (simulatorWindow == null)
        {
            outputText = "Open Device Simulator first";
            return;
        }

        try
        {
            var main = simulatorWindow.GetType().GetProperty("main").GetValue(simulatorWindow);
            if (main == null)
            {
                outputText = "No simulator main";
                return;
            }

            var screenSim = main.GetType().GetProperty("ScreenSimulation").GetValue(main);
            if (screenSim == null)
            {
                outputText = "No screen simulation";
                return;
            }

            var sb = new System.Text.StringBuilder();

            // Get device info
            var deviceInfo = screenSim.GetType().GetField("m_DeviceInfo", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(screenSim);
            sb.AppendLine($"Device: {deviceInfo}");

            // Get screen resolution and DPI
            var width = screenSim.GetType().GetProperty("width").GetValue(screenSim);
            var height = screenSim.GetType().GetProperty("height").GetValue(screenSim);
            var dpi = screenSim.GetType().GetProperty("dpi").GetValue(screenSim);
            sb.AppendLine($"Resolution: {width} x {height}");
            sb.AppendLine($"DPI: {dpi}");

            // Get orientation settings
            var orientation = screenSim.GetType().GetProperty("orientation").GetValue(screenSim);
            var autoRotation = screenSim.GetType().GetProperty("AutoRotation").GetValue(screenSim);
            sb.AppendLine($"\nOrientation: {orientation}");
            sb.AppendLine($"Auto Rotation: {autoRotation}");

            // Get safe area
            var safeArea = screenSim.GetType().GetProperty("safeArea").GetValue(screenSim);
            sb.AppendLine($"\nSafe Area: {safeArea}");

            // Get screen mode
            var fullScreen = screenSim.GetType().GetProperty("fullScreen").GetValue(screenSim);
            var fullScreenMode = screenSim.GetType().GetProperty("fullScreenMode").GetValue(screenSim);
            sb.AppendLine($"\nFull Screen: {fullScreen}");
            sb.AppendLine($"Mode: {fullScreenMode}");

            outputText = sb.ToString();
        }
        catch (System.Exception e)
        {
            outputText = $"Error: {e.Message}";
        }
    }
}
