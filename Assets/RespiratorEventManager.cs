using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class RespiratorEventManager : MonoBehaviour
{
    public enum EventType { Brak, Pokretlo, Sekwencja, Rurka }
    public enum ButtonCountMode { Random4or6, Always4, Always6 }

    [System.Serializable]
    public class ColorButtonInfo
    {
        public string materialName; // np. "Red", "Blue"
        public string letter;       // np. "C", "N"
        public string hexColor;     // np. "#FF3333"
        public Material material;
    }

    [Header("Zarządzanie UI (Canvas)")]
    public GameObject panelDomyslny;
    public GameObject panelAlarmowy;
    public TextMeshProUGUI alarmText;
    public TextMeshProUGUI outputText;

    [Header("Ustawienia Zdarzeń")]
    public EventType currentEvent = EventType.Brak;

    [Header("Gniazdo Rurki")]
    public XRSocketInteractor gniazdoRurki;

    [Header("Generator Przycisków (Button Panel)")]
    public ButtonCountMode trybIlosciPrzyciskow = ButtonCountMode.Random4or6;
    public Transform buttonPanel;
    public GameObject buttonPrefab;

    [Tooltip("Pula 8 materiałów z folderu Button_posibble_colors")]
    public List<Material> pulaMaterialowKolorow = new List<Material>();

    // --- ZMIENNE POKRĘTŁA ---
    [HideInInspector] public int targetAngle;
    private float currentDialAngle = 0f;
    private float dialHoldTimer = 0f;
    private float requiredHoldTime = 2.0f;

    // --- ZMIENNE PRZYCISKÓW ---
    private List<string> aktywneLiteryPrzyciskow = new List<string>();
    private Dictionary<string, string> literaDoKoloruHex = new Dictionary<string, string>();
    [HideInInspector] public List<string> targetSequence = new List<string>();
    private List<string> wcisnietePrzyciski = new List<string>();
    private int currentSequenceIndex = 0;

    // --- ZMIENNE DLA WIZUALIZACJI BŁĘDU ---
    private bool isShowingError = false;
    private float errorTimer = 0f;

    // --- ZMIENNE BLOKADY GNIAZDA RURKI ---
    private float socketIgnoreTimer = 0f;

    void Awake()
    {
        if (gniazdoRurki == null)
        {
            gniazdoRurki = GetComponentInChildren<XRSocketInteractor>();
        }

        if (gniazdoRurki != null)
        {
            gniazdoRurki.selectEntered.AddListener(OnSocketSelectEntered);
        }

        // Generowanie przycisków na panelu przy starcie
        GenerujPrzyciskiNaPanelu();
    }

    void Start()
    {
        ResetujAlarm();
    }

    void Update()
    {
        if (socketIgnoreTimer > 0f)
        {
            socketIgnoreTimer -= Time.deltaTime;
        }

        if (outputText != null && panelAlarmowy != null && panelAlarmowy.activeSelf)
        {
            if (isShowingError)
            {
                errorTimer -= Time.deltaTime;
                if (errorTimer <= 0f)
                {
                    isShowingError = false;
                }
                return;
            }

            switch (currentEvent)
            {
                case EventType.Pokretlo:
                    float diff = Mathf.Abs(Mathf.DeltaAngle(currentDialAngle, targetAngle));
                    string statusPokretla = diff <= 5f ? $" <color=green>(Utrzymaj: {dialHoldTimer:F1}/{requiredHoldTime:F0}s)</color>" : "";
                    outputText.text = $"Odczyt: {Mathf.RoundToInt(currentDialAngle)}°{statusPokretla}";
                    break;

                case EventType.Sekwencja:
                    string wpisano = "Wpisano: ";
                    foreach (string btn in wcisnietePrzyciski)
                    {
                        wpisano += FormatujLiterePrzycisku(btn) + " ";
                    }
                    outputText.text = wpisano;
                    break;

                case EventType.Rurka:
                    outputText.text = "<color=red>Stan obwodu: Odłączony</color>";
                    break;
            }
        }
    }

    // --- DYNAMICZNY GENERATOR PRZYCISKÓW ---
    public void GenerujPrzyciskiNaPanelu()
    {
        if (buttonPanel == null)
        {
            buttonPanel = transform.Find("Button_panel") ?? 
                          transform.Find("Prefab_Respirator 1/Button_panel") ?? 
                          GetComponentInChildren<Transform>().Find("Button_panel");
        }

        if (buttonPanel == null)
        {
            Debug.LogWarning($"[RespiratorEventManager] Brak Button_panel na obiekcie {gameObject.name}");
            return;
        }

        // 1. Sprawdzamy szablon przycisku lub prefab
        GameObject template = buttonPrefab;
        if (template == null)
        {
            Transform existingBtn = buttonPanel.Find("Phisic_button");
            if (existingBtn != null)
            {
                template = existingBtn.gameObject;
            }
        }

        if (template == null)
        {
            Debug.LogWarning("[RespiratorEventManager] Brak szablonu Phisic_button lub buttonPrefab do generowania.");
            return;
        }

        // 2. Ładujemy materiały jeśli lista jest pusta
        InicjalizujPuleMaterialow();

        // 3. Określamy liczbę przycisków: 4 lub 6
        int buttonCount = 4;
        if (trybIlosciPrzyciskow == ButtonCountMode.Random4or6)
        {
            buttonCount = (Random.value < 0.5f) ? 4 : 6;
        }
        else if (trybIlosciPrzyciskow == ButtonCountMode.Always6)
        {
            buttonCount = 6;
        }

        // 4. Losujemy unikalne kolory z 8 dostępnych materiałów
        List<ColorButtonInfo> wybraneKolory = LosujUnikalneKolory(buttonCount);

        // 5. Usuwamy stare przyciski na panelu
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in buttonPanel)
        {
            if (child.name.StartsWith("Phisic_button"))
            {
                toDestroy.Add(child.gameObject);
            }
        }
        foreach (var obj in toDestroy)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        // 6. Generujemy przyciski w symetrycznej siatce na Button_panel
        aktywneLiteryPrzyciskow.Clear();
        literaDoKoloruHex.Clear();

        Vector3[] pozycje;
        Vector3 skalaPrzyciskow;

        if (buttonCount == 4)
        {
            // Siatka 2x2
            skalaPrzyciskow = new Vector3(0.18f, 20f, 0.18f);
            pozycje = new Vector3[]
            {
                new Vector3(-0.23f, 5.4f, -0.23f),
                new Vector3( 0.23f, 5.4f, -0.23f),
                new Vector3(-0.23f, 5.4f,  0.23f),
                new Vector3( 0.23f, 5.4f,  0.23f)
            };
        }
        else
        {
            // Siatka 2x3 (3 kolumny, 2 wiersze)
            skalaPrzyciskow = new Vector3(0.13f, 20f, 0.13f);
            pozycje = new Vector3[]
            {
                new Vector3(-0.28f, 5.4f, -0.20f),
                new Vector3( 0.00f, 5.4f, -0.20f),
                new Vector3( 0.28f, 5.4f, -0.20f),
                new Vector3(-0.28f, 5.4f,  0.20f),
                new Vector3( 0.00f, 5.4f,  0.20f),
                new Vector3( 0.28f, 5.4f,  0.20f)
            };
        }

        for (int i = 0; i < buttonCount; i++)
        {
            GameObject btnInstance = Instantiate(template, buttonPanel);
            btnInstance.name = $"Phisic_button_{wybraneKolory[i].letter}";
            btnInstance.transform.localPosition = pozycje[i];
            btnInstance.transform.localRotation = Quaternion.identity;
            btnInstance.transform.localScale = skalaPrzyciskow;
            btnInstance.SetActive(true);

            // Konfiguracja skryptu przycisku
            RespiratorPushButton pushScript = btnInstance.GetComponent<RespiratorPushButton>();
            if (pushScript == null) pushScript = btnInstance.AddComponent<RespiratorPushButton>();

            pushScript.buttonID = wybraneKolory[i].letter;
            pushScript.eventManager = this;
            pushScript.pushAxis = new Vector3(0, -1, 0);
            pushScript.maxPushDepthMeters = 0.015f;
            pushScript.buttonRadiusMeters = 0.035f;
            pushScript.activationThreshold = 0.80f;
            pushScript.resetThreshold = 0.30f;
            pushScript.returnSpeed = 20f;

            // Znalezienie ruchomej płytki i przypisanie wylosowanego materiału
            Transform plate = btnInstance.transform.Find("Button_plate") ?? 
                              btnInstance.transform.Find("button") ?? 
                              btnInstance.transform.GetChild(0);
            pushScript.buttonMesh = plate;

            if (plate != null)
            {
                MeshRenderer mr = plate.GetComponent<MeshRenderer>();
                if (mr != null && wybraneKolory[i].material != null)
                {
                    mr.material = wybraneKolory[i].material;
                    pushScript.normalColor = mr.material.color;
                }
            }

            aktywneLiteryPrzyciskow.Add(wybraneKolory[i].letter);
            literaDoKoloruHex[wybraneKolory[i].letter] = wybraneKolory[i].hexColor;
        }

        Debug.Log($"[RespiratorEventManager] Wygenerowano {buttonCount} przycisków: {string.Join(", ", aktywneLiteryPrzyciskow)} na {gameObject.name}");
    }

    private void InicjalizujPuleMaterialow()
    {
        if (pulaMaterialowKolorow.Count == 0)
        {
            #if UNITY_EDITOR
            string[] matNames = { "Aqua", "Blue", "Brown", "Green", "Orange", "Pink", "Red", "Yellow" };
            foreach (var name in matNames)
            {
                Material m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/Button_posibble_colors/{name}.mat");
                if (m != null) pulaMaterialowKolorow.Add(m);
            }
            #endif
        }
    }

    private List<ColorButtonInfo> LosujUnikalneKolory(int count)
    {
        // 8 bazowych definicji kolorów
        List<ColorButtonInfo> wszystkieKolory = new List<ColorButtonInfo>()
        {
            new ColorButtonInfo { materialName = "Red",    letter = "C", hexColor = "#FF3333" },
            new ColorButtonInfo { materialName = "Green",  letter = "Z", hexColor = "#33FF33" },
            new ColorButtonInfo { materialName = "Blue",   letter = "N", hexColor = "#3388FF" },
            new ColorButtonInfo { materialName = "Yellow", letter = "Y", hexColor = "#FFFF33" },
            new ColorButtonInfo { materialName = "Orange", letter = "P", hexColor = "#FF9900" },
            new ColorButtonInfo { materialName = "Pink",   letter = "R", hexColor = "#FF66CC" },
            new ColorButtonInfo { materialName = "Aqua",   letter = "A", hexColor = "#00FFFF" },
            new ColorButtonInfo { materialName = "Brown",  letter = "B", hexColor = "#995522" }
        };

        // Podpinamy materiały z puli
        foreach (var info in wszystkieKolory)
        {
            info.material = pulaMaterialowKolorow.Find(m => m != null && m.name.Equals(info.materialName, System.StringComparison.OrdinalIgnoreCase));
        }

        // Tasujemy pulę i wybieramy `count` bez powtórzeń
        for (int i = 0; i < wszystkieKolory.Count; i++)
        {
            int rnd = Random.Range(i, wszystkieKolory.Count);
            var temp = wszystkieKolory[i];
            wszystkieKolory[i] = wszystkieKolory[rnd];
            wszystkieKolory[rnd] = temp;
        }

        return wszystkieKolory.GetRange(0, Mathf.Min(count, wszystkieKolory.Count));
    }

    private string FormatujLiterePrzycisku(string letter)
    {
        if (literaDoKoloruHex.TryGetValue(letter, out string hex))
        {
            return $"<color={hex}>[{letter}]</color>";
        }
        return $"[{letter}]";
    }

    public void WywolajLosowyAlarm()
    {
        int los = Random.Range(1, 4);
        switch (los)
        {
            case 1: WywolajScenariuszA(); break;
            case 2: WywolajScenariuszB(); break;
            case 3: WywolajScenariuszC(); break;
        }
    }

    public void WywolajScenariuszA()
    {
        if (panelDomyslny != null) panelDomyslny.SetActive(false);
        if (panelAlarmowy != null) panelAlarmowy.SetActive(true);
        currentEvent = EventType.Pokretlo;
        targetAngle = Random.Range(0, 360);
        dialHoldTimer = 0f;

        if (alarmText != null)
        {
            alarmText.text = $"ALARM!\nUstaw ciśnienie PEEP na {targetAngle}°\n(Utrzymaj przez {requiredHoldTime:F0}s)";
        }
    }

    public void WywolajScenariuszB()
    {
        if (panelDomyslny != null) panelDomyslny.SetActive(false);
        if (panelAlarmowy != null) panelAlarmowy.SetActive(true);
        currentEvent = EventType.Sekwencja;
        targetSequence.Clear();
        wcisnietePrzyciski.Clear();
        currentSequenceIndex = 0;
        isShowingError = false;

        if (aktywneLiteryPrzyciskow.Count == 0)
        {
            aktywneLiteryPrzyciskow = new List<string> { "C", "Z", "N", "Y" };
        }

        string wyswietlanyTekst = "Odblokuj: ";
        for (int i = 0; i < 4; i++)
        {
            string wylosowanaLitera = aktywneLiteryPrzyciskow[Random.Range(0, aktywneLiteryPrzyciskow.Count)];
            targetSequence.Add(wylosowanaLitera);
            wyswietlanyTekst += FormatujLiterePrzycisku(wylosowanaLitera) + " ";
        }

        if (alarmText != null)
        {
            alarmText.text = $"ALARM!\n{wyswietlanyTekst}";
        }
    }

    public void WywolajScenariuszC()
    {
        if (panelDomyslny != null) panelDomyslny.SetActive(false);
        if (panelAlarmowy != null) panelAlarmowy.SetActive(true);
        currentEvent = EventType.Rurka;
        if (alarmText != null)
        {
            alarmText.text = "ALARM!\nRozszczelnienie! Sprawdź obwód oddechowy.";
        }

        socketIgnoreTimer = 0.5f;

        if (gniazdoRurki != null && gniazdoRurki.hasSelection)
        {
            var wpietaRurka = gniazdoRurki.interactablesSelected[0];
            gniazdoRurki.interactionManager.SelectCancel(gniazdoRurki, wpietaRurka);

            Rigidbody rb = wpietaRurka.transform.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(gniazdoRurki.transform.forward * 1.5f + Vector3.up * 0.2f, ForceMode.Impulse);
            }
        }
    }

    private void OnSocketSelectEntered(SelectEnterEventArgs args)
    {
        if (socketIgnoreTimer > 0f) return;

        if (currentEvent == EventType.Rurka)
        {
            PodlaczonoRurke();
        }
    }

    public void PodlaczonoRurke()
    {
        if (currentEvent == EventType.Rurka)
        {
            Debug.Log("[RespiratorEventManager] Wykryto podłączenie obwodu oddechowego!");
            ZglosSukcesZadania();
        }
    }

    public void CheckDialValue(float currentAngle)
    {
        if (currentEvent != EventType.Pokretlo) return;

        currentDialAngle = currentAngle;

        float diff = Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle));

        if (diff <= 5f)
        {
            dialHoldTimer += Time.deltaTime;
            if (dialHoldTimer >= requiredHoldTime)
            {
                ZglosSukcesZadania();
            }
        }
        else
        {
            dialHoldTimer = 0f;
        }
    }

    public void OnButtonPressed(string buttonLetter)
    {
        if (currentEvent != EventType.Sekwencja) return;
        if (isShowingError) return;

        wcisnietePrzyciski.Add(buttonLetter);

        if (currentSequenceIndex < targetSequence.Count && targetSequence[currentSequenceIndex] == buttonLetter)
        {
            currentSequenceIndex++;

            if (currentSequenceIndex >= targetSequence.Count)
            {
                ZglosSukcesZadania();
            }
        }
        else
        {
            Debug.Log("[RespiratorEventManager] Zły przycisk! Zaczynamy sekwencję od nowa.");

            isShowingError = true;
            errorTimer = 1.5f;
            if (outputText != null)
            {
                outputText.text = "<color=red>BŁĄD! Zła sekwencja.</color>";
            }

            currentSequenceIndex = 0;
            wcisnietePrzyciski.Clear();
        }
    }

    public void ZglosSukcesZadania()
    {
        Debug.Log("[RespiratorEventManager] Zadanie wykonane! Wracam do trybu domyślnego.");
        ResetujAlarm();
    }

    public void ResetujAlarm()
    {
        currentEvent = EventType.Brak;
        if (panelDomyslny != null) panelDomyslny.SetActive(true);
        if (panelAlarmowy != null) panelAlarmowy.SetActive(false);
        isShowingError = false;
        dialHoldTimer = 0f;
        if (outputText != null) outputText.text = "";
    }

    void OnDestroy()
    {
        if (gniazdoRurki != null)
        {
            gniazdoRurki.selectEntered.RemoveListener(OnSocketSelectEntered);
        }
    }
}
