using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

[System.Serializable]
public class StoryData
{
    public List<NodeData> nodes;
}

[System.Serializable]
public class NodeData
{
    public string node_id;
    public string background;
    public string bgm;

    [Header("Variables")]
    public string add_var_on_first_visit;
    public int add_value;
    public string set_var_on_first_visit;
    public int set_value;
    public string auto_next_node;
    public string add_var_once;
    public int add_var_once_value;

    [Header("Input System")]
    public bool requires_input;
    public string input_target_var;
    public string input_placeholder;

    [Header("System Command")]
    public bool trigger_reset;

    public List<DialogueLine> dialogues;
    public List<ChoiceData> choices;
}

[System.Serializable]
public class DialogueLine
{
    public string speaker;
    public string expression;
    public string position;
    public string text;
    public string voice_clip; // [BARU] Audio untuk dialog ini
}

[System.Serializable]
public class ChoiceData
{
    public string choice_text;
    public string target_node;

    [Header("Conditions")]
    public string req_var;
    public string req_operator;
    public int req_value;
    public string req_value_string;
}

public class DialogueManager : MonoBehaviour
{
    [Header("Data")]
    public TextAsset jsonStoryFile;

    // --- [FITUR BARU] SCREEN MANAGEMENT ---
    [Header("Screens")]
    public GameObject mainMenuPanel; // Panel Layar Awal
    public GameObject gamePanel;     // Panel Utama Game

    [Header("UI Text")]
    public TextMeshProUGUI speakerNameText;
    public TextMeshProUGUI dialogueText;

    [Header("UI Choices")]
    public Transform choicesContainer;
    public GameObject choiceButtonPrefab;

    [Header("UI Input")]
    public TMP_InputField playerInputField;

    [Header("UI Visuals")]
    public RawImage backgroundImage;
    public VideoPlayer backgroundVideo;
    public Image characterSprite;

    // --- [FITUR BARU] AUDIO SYSTEM ---
    [Header("Audio")]
    public AudioSource bgmSource;    // Untuk lagu BGM (diatur ke Loop)
    public AudioSource voiceSource;  // Untuk Voice Over
    public string mainMenuBgmName;   // [BARU] Nama file lagu untuk Main Menu
    private string currentBgmName = ""; // Mengecek BGM apa yang sedang jalan

    private Dictionary<string, NodeData> storyDictionary;
    private Vector2 defaultMaskPosition;
    private Vector3 defaultMaskScale;
    private NodeData currentNode;
    private int currentLineIndex = 0;

    private Dictionary<string, object> playerVars = new Dictionary<string, object>();
    private HashSet<string> visitedNodes = new HashSet<string>();

    void Start()
    {
        RectTransform maskRect = characterSprite.transform.parent.GetComponent<RectTransform>();
        defaultMaskPosition = maskRect.anchoredPosition;
        defaultMaskScale = maskRect.localScale;

        StoryData story = JsonUtility.FromJson<StoryData>(jsonStoryFile.text);
        storyDictionary = new Dictionary<string, NodeData>();
        foreach (NodeData node in story.nodes) storyDictionary.Add(node.node_id, node);

        playerInputField.gameObject.SetActive(false);

        // --- [FITUR BARU] Menampilkan Main Menu saat game pertama dibuka ---
        ShowMainMenu();
    }

    void Update()
    {
        // Jangan lanjut jika panel game tidak aktif
        if (!gamePanel.activeSelf) return;

        if (playerInputField.gameObject.activeSelf || choicesContainer.childCount > 0) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            DisplayNextLine();
        }
    }

    // --- [FITUR BARU] KONTROL MAIN MENU ---
    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        gamePanel.SetActive(false);

        // Hentikan suara karakter jika ada yang masih bicara
        if (voiceSource != null) voiceSource.Stop();

        // --- [FITUR BARU] PLAY MAIN MENU BGM ---
        if (!string.IsNullOrEmpty(mainMenuBgmName) && currentBgmName != mainMenuBgmName)
        {
            AudioClip menuBgmClip = Resources.Load<AudioClip>("Audio/BGM/" + mainMenuBgmName);
            if (menuBgmClip != null)
            {
                bgmSource.clip = menuBgmClip;
                bgmSource.loop = true;
                bgmSource.Play();
                currentBgmName = mainMenuBgmName;
                Debug.Log("[BGM] Playing Main Menu BGM: " + currentBgmName);
            }
            else
            {
                Debug.LogWarning("[BGM] Gagal meload BGM Main Menu dari Resources/Audio/BGM/" + mainMenuBgmName);
            }
        }
        else if (string.IsNullOrEmpty(mainMenuBgmName))
        {
            // Jika dikosongkan di inspector, matikan lagu
            if (bgmSource != null) bgmSource.Stop();
            currentBgmName = "";
        }
    }

    // Panggil fungsi ini dari Event "OnClick" tombol PLAY di Main Menu
    public void StartGame()
    {
        mainMenuPanel.SetActive(false);
        gamePanel.SetActive(true);

        // Reset data dari awal untuk jaga-jaga
        playerVars.Clear();
        visitedNodes.Clear();
        //playerVars["checkloopone"] = 3;

        PlayNode("HeartCake: Start loop");
    }

    public void PlayNode(string nodeID)
    {
        if (storyDictionary.ContainsKey(nodeID))
        {
            currentNode = storyDictionary[nodeID];
            currentLineIndex = 0;

            // --- [FITUR BARU] RESET DATA & EXIT KE MAIN MENU ---
            if (currentNode.trigger_reset)
            {
                playerVars.Clear();
                visitedNodes.Clear();
                Debug.Log("[Sistem] Memori dihapus! Kembali ke Layar Utama...");

                // Kembali ke menu utama
                ShowMainMenu();
                return; // Berhenti mengeksekusi node ini karena kita keluar ke menu
            }

            // --- [FITUR BARU] BGM SYSTEM ---
            if (!string.IsNullOrEmpty(currentNode.bgm) && currentNode.bgm != currentBgmName)
            {
                AudioClip newBgm = Resources.Load<AudioClip>("Audio/BGM/" + currentNode.bgm);
                if (newBgm != null)
                {
                    bgmSource.clip = newBgm;
                    bgmSource.loop = true;
                    bgmSource.Play();
                    currentBgmName = currentNode.bgm; // Simpan memori bgm saat ini
                    Debug.Log("[BGM] Playing: " + currentBgmName);
                }
                else
                {
                    Debug.LogWarning("[BGM] Gagal meload BGM dari Resources/Audio/BGM/" + currentNode.bgm);
                }
            }

            // --- BACKGROUND (GAMBAR/VIDEO) ---
            if (!string.IsNullOrEmpty(currentNode.background))
            {
                string cleanBgName = System.IO.Path.GetFileNameWithoutExtension(currentNode.background);
                VideoClip vidClip = Resources.Load<VideoClip>("Videos/" + cleanBgName);

                if (vidClip != null)
                {
                    backgroundVideo.clip = vidClip;
                    backgroundVideo.Play();
                    backgroundImage.texture = backgroundVideo.targetTexture;
                    backgroundImage.color = Color.white;
                }
                else
                {
                    string imagePath = "Sprites/Backgrounds/" + cleanBgName;
                    Texture2D bgTex = Resources.Load<Texture2D>(imagePath);

                    if (bgTex != null)
                    {
                        if (backgroundVideo != null) backgroundVideo.Stop();
                        backgroundImage.texture = bgTex;
                        backgroundImage.color = Color.white;
                    }
                }
            }

            // Variabel logic
            if (!visitedNodes.Contains(nodeID))
            {
                visitedNodes.Add(nodeID);
                if (!string.IsNullOrEmpty(currentNode.set_var_on_first_visit))
                    ModifyVar(currentNode.set_var_on_first_visit, currentNode.set_value, false);
                if (!string.IsNullOrEmpty(currentNode.add_var_once))
                    ModifyVar(currentNode.add_var_once, currentNode.add_var_once_value, true);
            }

            if (!string.IsNullOrEmpty(currentNode.add_var_on_first_visit))
            {
                ModifyVar(currentNode.add_var_on_first_visit, currentNode.add_value, true);
            }

            DisplayNextLine();
        }
        else { Debug.LogError("Node ID tidak ditemukan: " + nodeID); }
    }

    private void ModifyVar(string varName, int value, bool isAdd)
    {
        if (!playerVars.ContainsKey(varName)) playerVars[varName] = 0;
        if (isAdd) playerVars[varName] = (int)playerVars[varName] + value;
        else playerVars[varName] = value;
    }

    public void DisplayNextLine()
    {
        if (currentLineIndex < currentNode.dialogues.Count)
        {
            DialogueLine line = currentNode.dialogues[currentLineIndex];
            speakerNameText.text = line.speaker;
            dialogueText.text = line.text;

            // --- [FITUR BARU] VOICE OVER SYSTEM ---
            if (voiceSource != null) voiceSource.Stop(); // Matikan voice dialog sebelumnya

            if (!string.IsNullOrEmpty(line.voice_clip))
            {
                AudioClip voClip = Resources.Load<AudioClip>("Audio/Voices/" + line.voice_clip);
                if (voClip != null)
                {
                    voiceSource.clip = voClip;
                    voiceSource.loop = false; // Voice over tidak boleh nge-loop
                    voiceSource.Play();
                }
                else
                {
                    Debug.LogWarning("[Audio] Voice clip tidak ditemukan: Resources/Audio/Voices/" + line.voice_clip);
                }
            }

            // --- SPRITE KARAKTER ---
            string spriteName = line.expression;
            Sprite charImg = Resources.Load<Sprite>("Sprites/Characters/" + spriteName);

            if (charImg != null)
            {
                characterSprite.sprite = charImg;
                characterSprite.gameObject.SetActive(true);
                RectTransform maskRect = characterSprite.transform.parent.GetComponent<RectTransform>();

                switch (line.position)
                {
                    case "top_center":
                        maskRect.anchoredPosition = new Vector2(0, -200);
                        maskRect.localScale = new Vector3(0.7f, 0.7f, 1f);
                        break;
                    case "top_left":
                        maskRect.anchoredPosition = new Vector2(-100, 100);
                        break;
                    default:
                        maskRect.anchoredPosition = defaultMaskPosition;
                        maskRect.localScale = defaultMaskScale;
                        break;
                }
            }
            else
            {
                characterSprite.gameObject.SetActive(false);
            }

            currentLineIndex++;
        }
        else
        {
            if (currentNode.requires_input)
            {
                playerInputField.gameObject.SetActive(true);
                playerInputField.text = "";
                playerInputField.placeholder.GetComponent<TextMeshProUGUI>().text = currentNode.input_placeholder;
            }

            if (currentNode.choices != null && currentNode.choices.Count > 0)
            {
                ShowChoices();
            }
            else if (!string.IsNullOrEmpty(currentNode.auto_next_node))
            {
                PlayNode(currentNode.auto_next_node);
            }
        }
    }

    private void ShowChoices()
    {
        foreach (Transform child in choicesContainer) Destroy(child.gameObject);

        bool adaPilihanValid = false;
        string firstValidAutoJump = "";

        foreach (ChoiceData choice in currentNode.choices)
        {
            if (CheckCondition(choice))
            {
                adaPilihanValid = true;
                if (string.IsNullOrEmpty(choice.choice_text))
                {
                    if (string.IsNullOrEmpty(firstValidAutoJump)) firstValidAutoJump = choice.target_node;
                    continue;
                }

                GameObject btnObj = Instantiate(choiceButtonPrefab, choicesContainer);
                btnObj.GetComponentInChildren<TextMeshProUGUI>().text = choice.choice_text;

                if (visitedNodes.Contains(choice.target_node))
                {
                    btnObj.GetComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f);
                }

                string targetID = choice.target_node;
                btnObj.GetComponent<Button>().onClick.AddListener(() => {
                    ProcessInputBeforeJump();
                    foreach (Transform child in choicesContainer) Destroy(child.gameObject);
                    PlayNode(targetID);
                });
            }
        }

        if (!string.IsNullOrEmpty(firstValidAutoJump))
        {
            ProcessInputBeforeJump();
            PlayNode(firstValidAutoJump);
        }
        else if (!adaPilihanValid && !string.IsNullOrEmpty(currentNode.auto_next_node))
        {
            PlayNode(currentNode.auto_next_node);
        }
    }

    private void ProcessInputBeforeJump()
    {
        if (currentNode.requires_input && playerInputField.gameObject.activeSelf)
        {
            playerVars[currentNode.input_target_var] = playerInputField.text;
            playerInputField.gameObject.SetActive(false);
        }
    }

    private bool CheckCondition(ChoiceData choice)
    {
        if (string.IsNullOrEmpty(choice.req_var)) return true;
        if (!playerVars.ContainsKey(choice.req_var)) return false;

        object varValue = playerVars[choice.req_var];

        if (varValue is string textValue)
        {
            string targetText = choice.req_value_string.ToLower();
            string currentText = textValue.ToLower();
            switch (choice.req_operator)
            {
                case "contains": return currentText.Contains(targetText);
                case "not_contains": return !currentText.Contains(targetText);
                case "==": return currentText == targetText;
                default: return true;
            }
        }
        else if (varValue is int intValue)
        {
            switch (choice.req_operator)
            {
                case "==": return intValue == choice.req_value;
                case ">=": return intValue >= choice.req_value;
                case "<=": return intValue <= choice.req_value;
                case ">": return intValue > choice.req_value;
                case "<": return intValue < choice.req_value;
                case "!=": return intValue != choice.req_value;
                default: return true;
            }
        }
        return true;
    }
}