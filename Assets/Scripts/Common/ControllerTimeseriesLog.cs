using System.Globalization;
using Valve.VR;

/// <summary>Shared controller button columns for controller_timeseries.csv (0 = not pressed, 1 = pressed at sample time).</summary>
public static class ControllerTimeseriesLog
{
    public const string PoseColumnsHeader =
        "position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,velocity_linear,velocity_angular";

    public const string ButtonColumnsHeader =
        "trigger_pressed,grip_pressed,pinch_pressed,interact_ui_pressed,teleport_pressed," +
        "touchpad_click_pressed,touchpad_left_pressed,touchpad_right_pressed," +
        "snap_turn_left_pressed,snap_turn_right_pressed";

    public struct Sample
    {
        public int TriggerPressed;
        public int GripPressed;
        public int PinchPressed;
        public int InteractUiPressed;
        public int TeleportPressed;
        public int TouchpadClickPressed;
        public int TouchpadLeftPressed;
        public int TouchpadRightPressed;
        public int SnapTurnLeftPressed;
        public int SnapTurnRightPressed;
    }

    public static Sample Capture(SteamVR_Input_Sources hand, float triggerPressThreshold, float touchpadSideThreshold = 0.5f)
    {
        var sample = new Sample();
        if (SteamVR.initializedState != SteamVR.InitializedStates.InitializeSuccess)
            return sample;

        var squeeze = SteamVR_Actions.default_Squeeze;
        if (squeeze != null && squeeze.activeBinding)
            sample.TriggerPressed = squeeze.GetAxis(hand) >= triggerPressThreshold ? 1 : 0;

        sample.GripPressed = BoolPressed(SteamVR_Actions.default_GrabGrip, hand);
        sample.PinchPressed = BoolPressed(SteamVR_Actions.default_GrabPinch, hand);
        sample.InteractUiPressed = BoolPressed(SteamVR_Actions.default_InteractUI, hand);
        sample.TeleportPressed = BoolPressed(SteamVR_Actions.default_Teleport, hand);
        sample.TouchpadClickPressed = BoolPressed(SteamVR_Actions.default_TouchpadClick, hand);
        sample.SnapTurnLeftPressed = BoolPressed(SteamVR_Actions.default_SnapTurnLeft, hand);
        sample.SnapTurnRightPressed = BoolPressed(SteamVR_Actions.default_SnapTurnRight, hand);

        var touchpad = SteamVR_Actions.default_TouchpadLeftRight;
        if (touchpad != null && touchpad.activeBinding)
        {
            float x = touchpad.GetAxis(hand).x;
            if (x <= -touchpadSideThreshold)
                sample.TouchpadLeftPressed = 1;
            else if (x >= touchpadSideThreshold)
                sample.TouchpadRightPressed = 1;
        }

        return sample;
    }

    public static string FormatButtonColumns(Sample sample)
    {
        return string.Join(",",
            sample.TriggerPressed.ToString(CultureInfo.InvariantCulture),
            sample.GripPressed.ToString(CultureInfo.InvariantCulture),
            sample.PinchPressed.ToString(CultureInfo.InvariantCulture),
            sample.InteractUiPressed.ToString(CultureInfo.InvariantCulture),
            sample.TeleportPressed.ToString(CultureInfo.InvariantCulture),
            sample.TouchpadClickPressed.ToString(CultureInfo.InvariantCulture),
            sample.TouchpadLeftPressed.ToString(CultureInfo.InvariantCulture),
            sample.TouchpadRightPressed.ToString(CultureInfo.InvariantCulture),
            sample.SnapTurnLeftPressed.ToString(CultureInfo.InvariantCulture),
            sample.SnapTurnRightPressed.ToString(CultureInfo.InvariantCulture));
    }

    static int BoolPressed(SteamVR_Action_Boolean action, SteamVR_Input_Sources hand)
    {
        if (action == null || !action.activeBinding)
            return 0;
        return action.GetState(hand) ? 1 : 0;
    }
}
