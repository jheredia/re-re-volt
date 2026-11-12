using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Presentation
{
    /// <summary>Viewport-local, presentation-only dials fed by measured vehicle telemetry.</summary>
    public sealed class RaceTelemetryDial
    {
        public const float MaximumSpeedKph = 320f;
        public const float MaximumRearWheelRpm = 12000f;
        private const float SpeedDialFullScaleKph = 120f;
        private const float RearWheelDialFullScaleRpm = 1500f;

        private readonly RectTransform speedNeedle;
        private readonly RectTransform wheelNeedle;
        private readonly TextMeshProUGUI speedValue;
        private readonly TextMeshProUGUI wheelValue;
        private int lastSpeed = -1;
        private int lastWheelRpm = -1;

        private RaceTelemetryDial(RectTransform speedNeedle, RectTransform wheelNeedle,
            TextMeshProUGUI speedValue, TextMeshProUGUI wheelValue)
        {
            this.speedNeedle = speedNeedle;
            this.wheelNeedle = wheelNeedle;
            this.speedValue = speedValue;
            this.wheelValue = wheelValue;
        }

        public static RaceTelemetryDial Create(Transform viewport, int localCount)
        {
            bool compact = localCount >= 3;
            float width = localCount == 1 ? 392f : compact ? 270f : 336f;
            float height = localCount == 1 ? 188f : compact ? 136f : 160f;
            float radius = localCount == 1 ? 66f : compact ? 43f : 55f;
            GameObject panel = Ui("Race Telemetry", viewport, true);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(compact ? -12f : -18f, compact ? 12f : 18f);
            rect.sizeDelta = new Vector2(width, height);
            panel.GetComponent<Image>().color = new Color(.025f, .055f, .065f, .88f);

            float centerY = height * .48f;
            CreateGauge(panel.transform, new Vector2(width * .27f, centerY), radius,
                "SPEED", "km/h", "120+", compact, new Color(.98f, .68f, .14f),
                out RectTransform speedHand, out TextMeshProUGUI speedText);
            CreateGauge(panel.transform, new Vector2(width * .73f, centerY), radius,
                "REAR WHEEL", "RPM", "1.5k+", compact, new Color(.28f, .83f, .79f),
                out RectTransform wheelHand, out TextMeshProUGUI wheelText);
            var dial = new RaceTelemetryDial(speedHand, wheelHand, speedText, wheelText);
            dial.SetTelemetry(0f, 0f);
            return dial;
        }

        public void SetTelemetry(float speedKph, float rearWheelRpm)
        {
            float speed = Bounded(speedKph, MaximumSpeedKph);
            float rpm = Bounded(rearWheelRpm, MaximumRearWheelRpm);
            speedNeedle.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(135f, -135f, speed / SpeedDialFullScaleKph));
            wheelNeedle.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(135f, -135f, rpm / RearWheelDialFullScaleRpm));
            int speedRounded = Mathf.RoundToInt(speed);
            int rpmRounded = Mathf.RoundToInt(rpm);
            if (speedRounded != lastSpeed) { speedValue.text = speedRounded.ToString(); lastSpeed = speedRounded; }
            if (rpmRounded != lastWheelRpm) { wheelValue.text = rpmRounded.ToString(); lastWheelRpm = rpmRounded; }
        }

        public static float Bounded(float value, float maximum) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, maximum);

        private static void CreateGauge(Transform parent, Vector2 center, float radius,
            string caption, string units, string maximum, bool compact, Color accent,
            out RectTransform needle, out TextMeshProUGUI value)
        {
            GameObject gauge = Ui(caption + " Dial", parent);
            RectTransform gaugeRect = gauge.GetComponent<RectTransform>();
            gaugeRect.anchorMin = gaugeRect.anchorMax = Vector2.zero;
            gaugeRect.pivot = new Vector2(.5f, .5f);
            gaugeRect.anchoredPosition = center;
            gaugeRect.sizeDelta = Vector2.one * radius * 2f;

            for (int i = 0; i <= 10; i++)
            {
                float angle = Mathf.Lerp(225f, -45f, i / 10f) * Mathf.Deg2Rad;
                GameObject tick = Ui("Tick", gauge.transform, true);
                RectTransform tickRect = tick.GetComponent<RectTransform>();
                tickRect.anchorMin = tickRect.anchorMax = new Vector2(.5f, .5f);
                tickRect.sizeDelta = new Vector2(compact ? 2f : 3f, compact ? 7f : 10f);
                tickRect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius - 8f);
                tickRect.localEulerAngles = new Vector3(0f, 0f, angle * Mathf.Rad2Deg - 90f);
                tick.GetComponent<Image>().color = i >= 8 ? accent : new Color(.58f, .71f, .7f, .8f);
            }

            GameObject hand = Ui("Needle", gauge.transform, true);
            needle = hand.GetComponent<RectTransform>();
            needle.anchorMin = needle.anchorMax = new Vector2(.5f, .5f);
            needle.pivot = new Vector2(.5f, 0f);
            needle.anchoredPosition = Vector2.zero;
            needle.sizeDelta = new Vector2(compact ? 3f : 4f, radius - 17f);
            hand.GetComponent<Image>().color = accent;

            Text(gauge.transform, caption, compact ? 11f : 14f, accent,
                new Vector2(0f, radius - (compact ? 7f : 9f)), new Vector2(radius * 2.2f, 22f));
            value = Text(gauge.transform, "Value", compact ? 24f : 33f, Color.white,
                new Vector2(0f, -radius * .31f), new Vector2(radius * 1.7f, 40f));
            Text(gauge.transform, units, compact ? 10f : 12f, new Color(.72f, .83f, .82f),
                new Vector2(0f, -radius * .71f), new Vector2(radius * 1.7f, 20f));
            Text(gauge.transform, "0", compact ? 9f : 11f, accent,
                new Vector2(-radius * .72f, -radius * .67f), new Vector2(30f, 18f));
            TextMeshProUGUI maximumLabel = Text(gauge.transform, "Maximum", compact ? 9f : 11f, accent,
                new Vector2(radius * .72f, -radius * .67f), new Vector2(35f, 18f));
            maximumLabel.text = maximum;
        }

        private static GameObject Ui(string name, Transform parent, bool image = false)
        {
            GameObject item = image
                ? new GameObject(name, typeof(RectTransform), typeof(Image))
                : new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            if (image) item.GetComponent<Image>().raycastTarget = false;
            return item;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, float size, Color color,
            Vector2 position, Vector2 dimensions)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.text = name == "Value" ? string.Empty : name;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }
    }
}
