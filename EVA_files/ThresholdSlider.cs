using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
/// <summary>
/// Adds a threshold indicator to a Slider UI
/// </summary>
public class ThresholdSlider : MonoBehaviour
{
    /// <summary>
    /// Indicator object to match threshhold
    /// </summary>
    [SerializeField] private RectTransform indicator;

    private Slider slider;
    private PaletteGraphic fill;

    private const float buffer = 0.01f;

    private float threshold;

    /// <summary>
    /// threshold of the slider (0, 1)
    /// </summary>
    public float Threshold
    {
        private get { return threshold; }
        set
        {
            threshold = value;
            UpdateIndicator();
        }
    }

    /// <summary>
    /// value of the slider
    /// </summary>
    public float Value {
        get { return slider.value; }
        set { slider.value = value; }   
    }

    /// <summary>
    /// calculates the percenatge of the slider filled ABOVE the threshold
    /// </summary>
    public float PercentMax { get {
            if(Threshold >= 1) { return 0; }
            return Mathf.Max(0, (Value - Threshold) / (1 - Threshold)); }
    }

    /// <summary>
    /// calculates the percentage of the slider filled BELOW the threshold
    /// </summary>
    public float PercentThreshold { get {
            if(Threshold == 0) { return 0; } //division by zero
            return Mathf.Min(1, Value / Threshold); }
    }


    private void Awake()
    {
        slider = GetComponent<Slider>();
        fill = slider.fillRect.GetComponent<PaletteGraphic>();
    }

    private void Update()
    {
        fill.paletteColor = PercentMax > 0 ?  PaletteColor.AccentTwo : PaletteColor.BaseDark;
        fill.Apply();
    }

    public void UpdateIndicator()
    {
        //adjust to match the Threshold value
        switch (slider.direction)
        {
            default:
            case Slider.Direction.BottomToTop:
                indicator.anchorMax = new Vector2(indicator.anchorMax.x, Threshold + buffer);
                indicator.anchorMin = new Vector2(indicator.anchorMin.x, Threshold - buffer);
                break;
            case Slider.Direction.TopToBottom:
                indicator.anchorMax = new Vector2(indicator.anchorMax.x, 1 - Threshold - buffer);
                indicator.anchorMin = new Vector2(indicator.anchorMin.x, 1 - Threshold + buffer);
                break;
            case Slider.Direction.RightToLeft:
                indicator.anchorMax = new Vector2(1 - Threshold - buffer, indicator.anchorMin.y);
                indicator.anchorMin = new Vector2(1 - Threshold + buffer, indicator.anchorMin.y);
                break;
            case Slider.Direction.LeftToRight:
                indicator.anchorMax = new Vector2(Threshold + buffer, indicator.anchorMin.y);
                indicator.anchorMin = new Vector2(Threshold - buffer, indicator.anchorMin.y);
                break;
        }

        //center the indicator to match new anchors
        indicator.anchoredPosition = Vector2.zero;
    }
}
