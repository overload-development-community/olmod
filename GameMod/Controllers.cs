using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace GameMod
{
    class Controllers
    {
        public static string m_serialized_data = String.Empty;
        public static float m_menu_sensitivity;
        public static float m_menu_deadzone;
        public static List<Controller> controllers = new List<Controller>();

        public class Controller
        {

            public List<Axis> axes = new List<Axis>();

            public class Axis
            {
                public float sensitivity = 1;
                public float deadzone = 1;
            }
        }

        public static void SetAxisSensitivity(int controller, int axis, float sensitivity)
        {
            Overload.Controller controller1 = Controls.m_controllers[controller];
            if (axis == -1)
            {
                for (axis = 0; axis < controller1.m_joystick.axisCount; axis++)
                {
                    SetAxisSensitivity(controller, axis, sensitivity);
                }
                return;
            }
            if (controller1.isConnected)
            {
                float sens = (float)sensitivity;
                sens *= 0.0145f;
                sens += 0.75f;
                controller1.m_joystick.calibrationMap.GetAxis(axis).sensitivity = sens;
                controller1.m_joystick.calibrationMap.GetAxis(axis).sensitivityType = Rewired.AxisSensitivityType.Multiplier;
                Controllers.controllers[controller].axes[axis].sensitivity = sensitivity;
            }
        }

        public static void SetAxisDeadzone(int controller, int axis, float dz_index)
        {
            Overload.Controller controller1 = Controls.m_controllers[controller];
            if (axis == -1)
            {
                for (axis = 0; axis < controller1.m_joystick.axisCount; axis++)
                {
                    SetAxisDeadzone(controller, axis, dz_index);
                }
                return;
            }
            if (controller1.isConnected)
            {
                controller1.m_joystick.calibrationMap.GetAxis(axis).deadZone = (float)dz_index / 200f;
                Controllers.controllers[controller].axes[axis].deadzone = dz_index;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
    class Controllers_UIElement_DrawControlsMenu
    {
        static void DrawItem(UIElement uie, ref Vector2 position)
        {
            Controllers.m_menu_sensitivity = Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].sensitivity;
            Controllers.m_menu_deadzone = Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].deadzone;
            uie.SelectAndDrawSliderItem(Loc.LS("AXIS SENSITIVITY"), position, 7, (float)Controllers.m_menu_sensitivity / 100f);
            position.y += 62f;
            uie.SelectAndDrawSliderItem(Loc.LS("AXIS DEADZONE"), position, 8, (float)Controllers.m_menu_deadzone / 100f);
            position.y += 62f;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {

            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "AXIS SENSITIVITY")
                    state = 1; // Start code block skip at Axis Sensitivity

                if (state == 1 && code.opcode == OpCodes.Ldstr && (string)code.operand == "APPLY TO ALL AXES")
                {
                    state = 2; // End code block skip
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Controllers_UIElement_DrawControlsMenu), "DrawItem"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                }
                else if (state == 1)
                {
                    continue;
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
    class Controllers_MenuManager_ControlsOptionsUpdate
    {

        static void PatchMenu()
        {
            Controllers.m_menu_sensitivity = Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].sensitivity;
            Controllers.m_menu_deadzone = Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].deadzone;
            switch (UIManager.m_menu_selection)
            {
                case 7:
                    if (UIManager.m_menu_use_mouse)
                    {
                        if (UIElement.SliderValid)
                        {
                            Controllers.m_menu_sensitivity = (int)((double)UIElement.SliderPos * 100.0);
                            Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].sensitivity = Controllers.m_menu_sensitivity;
                            Controllers.SetAxisSensitivity(MenuManager.m_calibration_current_controller, MenuManager.m_calibration_current_axis, Controllers.m_menu_sensitivity);
                            if (Input.GetMouseButtonDown(0))
                            {
                                MenuManager.PlayCycleSound(1f, (float)((double)UIElement.SliderPos * 5.0 - 3.0));
                                break;
                            }
                            break;
                        }
                        break;
                    }
                    Controllers.m_menu_sensitivity = MenuManager.AdjustSensitivity(UIManager.m_select_dir, (int)Controllers.m_menu_sensitivity);
                    Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].sensitivity = Controllers.m_menu_sensitivity;
                    Controllers.SetAxisSensitivity(MenuManager.m_calibration_current_controller, MenuManager.m_calibration_current_axis, Controllers.m_menu_sensitivity);
                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                    break;
                case 8:
                    if (UIManager.m_menu_use_mouse)
                    {
                        if (UIElement.SliderValid)
                        {
                            Controllers.m_menu_deadzone = (int)((double)UIElement.SliderPos * 100.0);
                            Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].deadzone = Controllers.m_menu_deadzone;
                            Controllers.SetAxisDeadzone(MenuManager.m_calibration_current_controller, MenuManager.m_calibration_current_axis, Controllers.m_menu_deadzone);
                            if (Input.GetMouseButtonDown(0))
                            {
                                MenuManager.PlayCycleSound(1f, (float)((double)UIElement.SliderPos * 5.0 - 3.0));
                                break;
                            }
                            break;
                        }
                        break;
                    }
                    Controllers.m_menu_deadzone = MenuManager.AdjustSensitivity(UIManager.m_select_dir, (int)Controllers.m_menu_deadzone);
                    Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].deadzone = Controllers.m_menu_deadzone;
                    Controllers.SetAxisDeadzone(MenuManager.m_calibration_current_controller, MenuManager.m_calibration_current_axis, Controllers.m_menu_deadzone);
                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                    break;
                case 6:
                    Controllers.SetAxisSensitivity(MenuManager.m_calibration_current_controller, -1, Controllers.m_menu_sensitivity);
                    Controllers.SetAxisDeadzone(MenuManager.m_calibration_current_controller, -1, Controllers.m_menu_deadzone);
                    break;
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Controller), "SetAxisSensitivity"))
                {
                    state++;
                    if (state == 2)
                    {
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MenuManager), "m_calibration_current_controller")); // Current controller
                        yield return new CodeInstruction(OpCodes.Ldc_I4, -1); // All axes
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Controllers), "m_menu_sensitivity"));
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Controllers), "SetAxisSensitivity"));
                        continue;
                    }
                }

                if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Controller), "SetAxisDeadzone"))
                {
                    state++;
                    if (state == 4)
                    {
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(MenuManager), "m_calibration_current_controller")); // Current controller
                        yield return new CodeInstruction(OpCodes.Ldc_I4, -1); // All axes
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(Controllers), "m_menu_deadzone"));
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Controllers), "SetAxisDeadzone"));
                        continue;
                    }
                }

                if (code.opcode == OpCodes.Stloc_S && ((LocalBuilder)code.operand).LocalType == typeof(Overload.Controller))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Controllers_MenuManager_ControlsOptionsUpdate), "PatchMenu"));
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(PilotManager), "Select", new Type[] { typeof(string) })]
    class Controllers_PilotManager_Select
    {

        static void ReadControlData()
        {
            string fn = PilotManager.FileName(PilotFileType.CONFIG) + "mod";
            for (int i = 0; i < Controls.m_controllers.Count; i++)
            {
                Controllers.controllers.Add(new Controllers.Controller
                {
                    axes = new List<Controllers.Controller.Axis>()
                });
                for (int j = 0; j < Controls.m_controllers[i].m_axis_count; j++)
                {
                    int dz_index = Controls.m_controllers[i].GetAxisDeadzone(j);
                    int sens_index = Controls.m_controllers[i].GetAxisSensitivity(j);
                    float sens = ((RWInput.sens_multiplier[sens_index] - 0.75f) / 1.45f) * 100f;
                    float deadzone = (Controls.DEADZONE_ADDITIONAL[dz_index] / 0.5f) * 100f;

                    Controllers.controllers[i].axes.Add(new Controllers.Controller.Axis()
                    {
                        sensitivity = sens,
                        deadzone = deadzone
                    });
                }
            }
            Controllers.m_serialized_data = Platform.ReadTextUserData(fn);
            if (Controllers.m_serialized_data == null)
            {
                Controllers.m_serialized_data = String.Empty;
                Debug.Log("No existing .xconfigmod file");
                return;
            }
            using (MemoryStream memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(Controllers.m_serialized_data)))
            {
                using (StreamReader streamReader = new StreamReader(memoryStream))
                {
                    ReadControlDataFromStream(streamReader);
                }
            }
        }

        static void ReadControlDataFromStream(StreamReader sr)
        {
            string text = sr.ReadLine(); // 1
            if (!RUtility.StringStartsWith(text, Controls.CONFIG_KEY))
            {
                Debug.Log("olmod controls config save file has an incorrect key: " + text);
                return;
            }
            int numControllers = int.Parse(sr.ReadLine());
            int[] controllers = new int[numControllers];
            for (int i = 0; i < numControllers; i++)
            {
                string controllerName = sr.ReadLine();
                int numAxes = int.Parse(sr.ReadLine());
                for (int j = 0; j < numAxes; j++)
                {
                    Controllers.controllers[i].axes[j].deadzone = float.Parse(sr.ReadLine(), CultureInfo.InvariantCulture);
                    Controllers.controllers[i].axes[j].sensitivity = float.Parse(sr.ReadLine(), CultureInfo.InvariantCulture);
                    Controllers.SetAxisDeadzone(i, j, Controllers.controllers[i].axes[j].deadzone);
                    Controllers.SetAxisSensitivity(i, j, Controllers.controllers[i].axes[j].sensitivity);
                }
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Platform), "CloseMountPoint"))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Controllers_PilotManager_Select), "ReadControlData"));
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(Controls), "SaveControlData")]
    class Controllers_Controls_SaveControlData
    {

        static void Postfix(string filename)
        {
            string fn = filename + "mod";
            Debug.Log("SaveControlDataMod: " + fn);
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (StreamWriter streamWriter = new StreamWriter(memoryStream))
                    {
                        WriteControlDataToStream(streamWriter);
                        streamWriter.Flush();
                        memoryStream.Position = 0L;
                        using (StreamReader streamReader = new StreamReader(memoryStream))
                        {
                            string text = streamReader.ReadToEnd();
                            //if (Controllers.m_serialized_data != text)
                            //{
                            Controllers.m_serialized_data = text;
                            Platform.WriteTextUserData(fn, Controllers.m_serialized_data);
                            //}
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                GameManager.DebugOut("Error in ControlsMod.SaveControlData: " + ex.Message, 15f);
            }
        }

        static void WriteControlDataToStream(StreamWriter w)
        {
            w.WriteLine(Controls.CONFIG_KEY + Controls.VERSION);
            bool[] array = new bool[Controls.m_controllers.Count];
            array.Populate(true);
            w.WriteLine(Controls.m_controllers.Count);
            for (int i = 0; i < Controls.m_controllers.Count; i++)
            {
                Overload.Controller controller = Controls.m_controllers[i];
                w.WriteLine((!array[i]) ? string.Empty : controller.name);
                if (array[i])
                {
                    w.WriteLine(controller.m_axis_count);
                    for (int j = 0; j < controller.m_axis_count; j++)
                    {
                        w.WriteLine(Controllers.controllers[i].axes[j].deadzone.ToStringInvariantCulture());
                        w.WriteLine(Controllers.controllers[i].axes[j].sensitivity.ToStringInvariantCulture());
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(Controls), "OnControllerConnected")]
    class Controllers_Controls_OnControllerConnected
    {
        static void DeadzoneHelper(int controller, int axis)
        {
            Controllers.SetAxisDeadzone(controller, axis, Controllers.controllers[controller].axes[axis].deadzone);
        }

        static void SensitivityHelper(int controller, int axis)
        {
            Controllers.SetAxisSensitivity(controller, axis, Controllers.controllers[controller].axes[axis].sensitivity);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Controller), "SetAxisDeadzone"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_3); // Current controller
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 4); // Current axis
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Controllers_Controls_OnControllerConnected), "DeadzoneHelper"));
                    continue;
                }

                if (code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Controller), "SetAxisSensitivity"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_3); // Current controller
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 4); // Current axis
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Controllers_Controls_OnControllerConnected), "SensitivityHelper"));
                    continue;
                }

                yield return code;
            }
        }
    }

}
