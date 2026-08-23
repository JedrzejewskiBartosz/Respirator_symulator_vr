using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class HospitalManager : MonoBehaviour
{
    [Header("Zarządzanie Respiratorami")]
    [Tooltip("Przeciągnij tutaj wszystkie 3 respiratory ze sceny")]
    public List<RespiratorEventManager> respiratory;

    [Tooltip("Maksymalna liczba zepsutych maszyn w jednym momencie")]
    public int maksAktywnychAwarii = 2;

    [Tooltip("Minimalny czas (w sekundach) między kolejnymi awariami")]
    public float minCzasAwarii = 5f;
    [Tooltip("Maksymalny czas (w sekundach) między kolejnymi awariami")]
    public float maxCzasAwarii = 15f;

    private float timerAwarii = 0f;

    [Header("UI Zegarka (Wrist Menu)")]
    [Tooltip("Pola tekstowe pod przyciskami teleportacji. Musi ich być tyle samo co respiratorów!")]
    public TextMeshProUGUI[] tekstyStatusu;

    void Start()
    {
        ResetujTimer();
    }

    void Update()
    {
        ObslugaLosowychAwarii();
        AktualizujZegarek();
    }

    private void ObslugaLosowychAwarii()
    {
        timerAwarii -= Time.deltaTime;

        if (timerAwarii <= 0f)
        {
            SprobujWywolacAwarie();
            ResetujTimer(); // Losujemy czas do następnego uderzenia
        }
    }

    public void WylosujIAktywujAwarie()
    {
        SprobujWywolacAwarie();
    }

    public void SprobujWywolacAwarie()
    {
        int liczbaAwarii = 0;
        List<RespiratorEventManager> sprawneRespiratory = new List<RespiratorEventManager>();

        // Sprawdzamy, które respiratory są aktualnie zepsute, a które wolne
        foreach (var resp in respiratory)
        {
            if (resp == null) continue;

            if (resp.currentEvent != RespiratorEventManager.EventType.Brak)
            {
                liczbaAwarii++;
            }
            else
            {
                sprawneRespiratory.Add(resp);
            }
        }

        // Jeśli osiągnęliśmy limit awarii lub wszystkie maszyny są zepsute - przerywamy
        if (liczbaAwarii >= maksAktywnychAwarii || sprawneRespiratory.Count == 0)
        {
            return;
        }

        // Losujemy jeden ze sprawnych respiratorów i wywołujemy na nim awarię
        int losowyIndeks = Random.Range(0, sprawneRespiratory.Count);
        sprawneRespiratory[losowyIndeks].WywolajLosowyAlarm();

        Debug.Log($"[HospitalManager] Wywołano awarię na respiratorze {losowyIndeks + 1}!");
    }

    private void ResetujTimer()
    {
        timerAwarii = Random.Range(minCzasAwarii, maxCzasAwarii);
    }

    private void AktualizujZegarek()
    {
        if (tekstyStatusu == null || respiratory == null) return;
        if (tekstyStatusu.Length != respiratory.Count) return;

        for (int i = 0; i < respiratory.Count; i++)
        {
            var resp = respiratory[i];
            if (resp == null || tekstyStatusu[i] == null) continue;

            string status = "OK";
            Color kolorTekstu = Color.green;

            // Sprawdzamy event na danej maszynie
            if (resp.currentEvent != RespiratorEventManager.EventType.Brak)
            {
                kolorTekstu = Color.red;
                switch (resp.currentEvent)
                {
                    case RespiratorEventManager.EventType.Pokretlo:
                        status = "AWARIA: Ciśnienie PEEP";
                        break;
                    case RespiratorEventManager.EventType.Sekwencja:
                        status = "AWARIA: Blokada systemu";
                        break;
                    case RespiratorEventManager.EventType.Rurka:
                        status = "AWARIA: Rozszczelnienie";
                        break;
                }
            }

            // Nadpisujemy tekst na nadgarstku
            tekstyStatusu[i].color = kolorTekstu;
            tekstyStatusu[i].text = $"Respirator {i + 1}: {status}";
        }
    }
}
