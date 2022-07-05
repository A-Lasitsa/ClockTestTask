using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class Clock : MonoBehaviour
{
    bool adjustForTimezone = true; // Use device time zone? If false, uses utc
    bool repeatAlarmNextDay = false; // When alarm is triggered, set alarm for the same time the next day

    // UI elements
    public Canvas UICanvas;
    public RectTransform Analog;
    public RectTransform Digital;
    public RectTransform Alarm;

    public Text TimeText;
    public Transform HourHand;
    public Transform MinuteHand;
    public Transform SecondHand;
    public InputField AlarmField;
    public GameObject AlarmBackground;
    public Transform AlarmHourHand;
    public Transform AlarmMinuteHand;


    string[] timeSources = new string[] { "time.google.com", "pool.ntp.org" };
    DateTime dateTime = DateTime.MinValue;

    DateTime alarmTime = DateTime.MaxValue;

    bool hourHandDragging = false;
    bool touchDrag = false;
    Vector2 dragStart;
    Vector2 dragLast;
    float angle;
    Vector2 analogClockCenter;

    DeviceOrientation curOrientation;

    void Awake()
    {
        Application.targetFrameRate = 60;
        curOrientation = DeviceOrientation.LandscapeLeft;
    }
    void Start()
    {
        // Request time
        StartCoroutine(HourlyDateTimeUpdate());
        // Adjust for device time zone (if enabled, otherwise use utc)
        if (adjustForTimezone)
            dateTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime, TimeZoneInfo.Local);
        // Update clock, but not every frame
        StartCoroutine(ClockUpdate());
        // Device orientation check
        StartCoroutine(OrientationCheck());
        // For alarm
        analogClockCenter = new Vector2(Screen.width / 2 + Analog.anchoredPosition.x * UICanvas.scaleFactor, Screen.height / 2 + Analog.anchoredPosition.y * UICanvas.scaleFactor);
    }

    void Update()
    {
        // Advance time
        dateTime = dateTime.AddSeconds(Time.deltaTime);

        // Check if alarm is triggered
        if (dateTime >= alarmTime)
        {
            StartCoroutine(AlarmEffect());
            if (repeatAlarmNextDay)
                alarmTime = alarmTime.AddDays(1f);
            else
            {
                alarmTime = DateTime.MaxValue;
                AlarmField.text = "";
            }
        }

        // Hour hand drag alarm handling
        if (hourHandDragging)
        {
            // If hand is dragged, update alarm hands positions
            if (Input.GetMouseButton(0))
            {
                if (touchDrag)
                    dragLast = Input.touches[0].position;
                else
                    dragLast = Input.mousePosition;

                angle = -Vector2.SignedAngle(Vector2.up, dragLast - analogClockCenter);
                if (angle < 0)
                    angle = 360 + angle;
                AlarmHourHand.rotation = Quaternion.Euler(0, 0, -angle);
                AlarmMinuteHand.rotation = Quaternion.Euler(0, 0, -12 * (angle % 30));
            }
            else
            {
                // Stop dragging
                hourHandDragging = false;
                // Set alarm time based on hand angle
                alarmTime = DateTime.Today.AddHours(Mathf.Floor(angle / 30f)).AddMinutes(Mathf.Round(2f * (angle % 30f)));
                // If alarm is set for time in the past, add 12 hours to it
                if (dateTime > alarmTime)
                    alarmTime = alarmTime.AddHours(12f);

                // Adjust alarm text field
                AlarmField.text = $"{alarmTime.Hour:00}:{alarmTime.Minute:00}:{alarmTime.Second:00}";

                Debug.Log($"Alarm set for {alarmTime}");
            }
        }
    }

    public void SetAlarmFromText()
    {
        try
        {
            alarmTime = DateTime.Parse(AlarmField.text);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Invalid alarm time input.\n" + e);
            return;
        }
        // If alarm time is earlier than current time, set it for tomorrow
        if (alarmTime < dateTime)
            alarmTime = alarmTime.AddDays(1f);

        // Move alarm hands
        float m = alarmTime.Minute + alarmTime.Second * 0.01666667f;
        float h = alarmTime.Hour + m * 0.01666667f;
        AlarmMinuteHand.rotation = Quaternion.Euler(0, 0, -6 * m);
        AlarmHourHand.rotation = Quaternion.Euler(0, 0, -30 * h);

        Debug.Log($"Alarm set for {alarmTime}");
    }

    public void HourHandDrag()
    {
        hourHandDragging = true;
        if (Input.touchSupported & Input.touches.Length != 0)
        {
            dragStart = Input.touches[0].position;
            touchDrag = true;
        }
        else
        {
            dragStart = Input.mousePosition;
            touchDrag = false;
        }
    }

    // Request online time every hour
    IEnumerator HourlyDateTimeUpdate()
    {
        while (true)
        {
            DateTime newDateTime = DateTime.MinValue;
            foreach (var s in timeSources)
            {
                newDateTime = NTPTime.GetNTPTime(s);
                // Use the first valid online time
                if (newDateTime != DateTime.MinValue)
                {
                    dateTime = newDateTime;
                    Debug.Log($"Time successfully loaded from {s}.");
                    break;
                }
            }
            // If valid online time was not recieved, keep using current one or use system time if there is no current online time
            if (newDateTime == DateTime.MinValue)
            {
                if (dateTime == DateTime.MinValue)
                {
                    Debug.LogWarning("Failed to get online time from any source! Using local time.");
                    dateTime = DateTime.Now;
                }
                else
                {
                    Debug.LogWarning("Failed to update time from any source. Using current time estimate.");
                }
            }
            yield return new WaitForSeconds(3600f);
        }
    }

    // Update clock UI readings
    IEnumerator ClockUpdate()
    {
        while (true)
        {
            // Clocks
            TimeText.text = $"{dateTime.Hour:00}:{dateTime.Minute:00}:{dateTime.Second:00}";
            float s = dateTime.Second + dateTime.Millisecond * 0.001f;
            float m = dateTime.Minute + s * 0.01666667f;
            float h = dateTime.Hour + m * 0.01666667f;
            SecondHand.rotation = Quaternion.Euler(0, 0, -6 * s);
            MinuteHand.rotation = Quaternion.Euler(0, 0, -6 * m);
            HourHand.rotation = Quaternion.Euler(0, 0, -30 * h);

            // Alarm hands
            if (alarmTime == DateTime.MaxValue & !hourHandDragging)
            {
                AlarmMinuteHand.rotation = Quaternion.Euler(0, 0, -6 * m);
                AlarmHourHand.rotation = Quaternion.Euler(0, 0, -30 * h);
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    // Flicker background on alarm
    IEnumerator AlarmEffect()
    {
        Debug.Log("Alarm triggered.");
        // Max "i" must be even
        for (int i = 0; i < 16; i++)
        {
            if (AlarmBackground.activeInHierarchy)
                AlarmBackground.SetActive(false);
            else
                AlarmBackground.SetActive(true);
            yield return new WaitForSeconds(0.5f);
        }
    }

    IEnumerator OrientationCheck()
    {
        while (true)
        {
            if (Input.deviceOrientation != curOrientation)
            {
                curOrientation = Input.deviceOrientation;
                if (curOrientation == DeviceOrientation.LandscapeLeft || curOrientation == DeviceOrientation.LandscapeRight)
                {
                    Digital.anchoredPosition = new Vector2(220f, 120f);
                    Analog.anchoredPosition = new Vector2(-150f, 0f);
                    Alarm.anchoredPosition = new Vector2(220f, -50f);
                }
                else if (curOrientation == DeviceOrientation.Portrait)
                {
                    Digital.anchoredPosition = new Vector2(0f, 350f);
                    Analog.anchoredPosition = new Vector2(0f, 50f);
                    Alarm.anchoredPosition = new Vector2(0f, -300f);
                }
                analogClockCenter = new Vector2(Screen.width / 2 + Analog.anchoredPosition.x * UICanvas.scaleFactor, Screen.height / 2 + Analog.anchoredPosition.y * UICanvas.scaleFactor);
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
}
