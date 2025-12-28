using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Updates player score UI entries and sorts them by score.
/// </summary>
[DisallowMultipleComponent]
public class ScoreboardController : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Range(1, 3)] public int playerIndex = 1;
        [SerializeField] public Transform root;
        [SerializeField] public TMP_Text nameText;
        [SerializeField] public TMP_Text scoreText;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();
    [SerializeField, Tooltip("Fill missing name text with the default format when empty.")] private bool fillMissingNames = true;
    [SerializeField] private string defaultNameFormat = "Player {0}";

    private readonly Dictionary<int, int> _scores = new Dictionary<int, int>();

    public void SetScores(int score1, int score2, int score3, string name1 = null, string name2 = null, string name3 = null)
    {
        SetEntryInternal(1, score1, name1);
        SetEntryInternal(2, score2, name2);
        SetEntryInternal(3, score3, name3);
        SortEntries();
    }

    public void SetEntry(int playerIndex, int score, string name = null)
    {
        SetEntryInternal(playerIndex, score, name);
        SortEntries();
    }

    private void SetEntryInternal(int playerIndex, int score, string name)
    {
        Entry entry = FindEntry(playerIndex);
        if (entry == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(name))
        {
            if (entry.nameText)
            {
                entry.nameText.text = name;
            }
        }
        else if (fillMissingNames && entry.nameText && string.IsNullOrWhiteSpace(entry.nameText.text))
        {
            entry.nameText.text = string.Format(defaultNameFormat, playerIndex);
        }

        if (entry.scoreText)
        {
            entry.scoreText.text = score.ToString();
        }

        _scores[playerIndex] = score;
    }

    private void SortEntries()
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        List<Entry> sorted = new List<Entry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry == null || !ResolveRoot(entry))
            {
                continue;
            }
            sorted.Add(entry);
        }

        if (sorted.Count == 0)
        {
            return;
        }

        sorted.Sort(CompareEntries);
        for (int i = 0; i < sorted.Count; i++)
        {
            ResolveRoot(sorted[i]).SetSiblingIndex(i);
        }
    }

    private int CompareEntries(Entry a, Entry b)
    {
        int scoreA = GetScore(a);
        int scoreB = GetScore(b);
        int cmp = scoreB.CompareTo(scoreA);
        if (cmp != 0)
        {
            return cmp;
        }

        return a.playerIndex.CompareTo(b.playerIndex);
    }

    private int GetScore(Entry entry)
    {
        if (entry == null)
        {
            return 0;
        }

        if (_scores.TryGetValue(entry.playerIndex, out int score))
        {
            return score;
        }

        if (entry.scoreText && int.TryParse(entry.scoreText.text, out int parsed))
        {
            return parsed;
        }

        return 0;
    }

    private Entry FindEntry(int playerIndex)
    {
        if (entries == null)
        {
            return null;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry != null && entry.playerIndex == playerIndex)
            {
                return entry;
            }
        }

        return null;
    }

    private Transform ResolveRoot(Entry entry)
    {
        if (entry.root)
        {
            return entry.root;
        }

        if (entry.nameText)
        {
            return entry.nameText.transform;
        }

        if (entry.scoreText)
        {
            return entry.scoreText.transform;
        }

        return null;
    }
}
