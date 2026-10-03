using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
    public string voice_clip;
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

    [Header("Screens")]
    public Transform mainMenuPanel;
    public Transform gamePanel;

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
    public Image characterSprite;

    [Header("Audio")]
    public AudioSource bgmSource;
    public AudioSource voiceSource;
    public string mainMenuBgmName;
    private string currentBgmName = "";

    private Dictionary<string, NodeData> storyDictionary;
    private Vector2 defaultMaskPosition;
    private Vector3 defaultMaskScale;
    private NodeData currentNode;
    private int currentLineIndex = 0;

    private Dictionary<string, object> playerVars = new Dictionary<string, object>();
    private HashSet<string> visitedNodes = new HashSet<string>();

    // [FITUR BARU] Kunci untuk menghentikan loop error ribuan kali
    private bool isGameReady = false;

    void Start()
    {
        try
        {
            if (mainMenuPanel == null)
            {
                mainMenuPanel = GameObject.Find("MainMenuPanel").transform;
            }

            if (gamePanel == null)
            {
                gamePanel = GameObject.Find("GamePanel").transform;
            }

            // Mengumpulkan semua error dalam satu keranjang
            string daftarKosong = "";

            if (mainMenuPanel == null) daftarKosong += "- Main Menu Panel\n";
            if (gamePanel == null) daftarKosong += "- Game Panel\n";
            if (playerInputField == null) daftarKosong += "- Player Input Field\n";
            if (choicesContainer == null) daftarKosong += "- Choices Container\n";
            if (dialogueText == null) daftarKosong += "- Dialogue Text\n";
            if (characterSprite == null) daftarKosong += "- Character Sprite\n";
            if (jsonStoryFile == null) daftarKosong += "- Json Story File\n";

            // Kalau ada satu saja yang kosong, teriakkan semuanya sekaligus!
            if (daftarKosong != "")
            {
                throw new System.Exception("Script di objek [" + gameObject.name + "] punya slot kosong:\n" + daftarKosong);
            }

            RectTransform maskRect = characterSprite.transform.parent.GetComponent<RectTransform>();
            if (maskRect == null) throw new System.Exception("STRUKTUR UI SALAH di objek [" + gameObject.name + "]");

            defaultMaskPosition = maskRect.anchoredPosition;
            defaultMaskScale = maskRect.localScale;

            StoryData story = JsonUtility.FromJson<StoryData>(jsonStoryFile.text);
            if (story == null || story.nodes == null) throw new System.Exception("ERROR JSON!");

            storyDictionary = new Dictionary<string, NodeData>();
            foreach (NodeData node in story.nodes) storyDictionary.Add(node.node_id, node);

            playerInputField.gameObject.SetActive(false);

            ShowMainMenu();

            isGameReady = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("GAGAL START: " + e.Message);
            if (dialogueText != null)
            {
                dialogueText.text = "ERROR SYSTEM:\n" + e.Message;
            }
        }
    }

    void Update()
    {
        // [KUNCI RAHASIA] Jika Start gagal, hentikan Update agar tidak looping ribuan kali!
        if (!isGameReady) return;

        if (!gamePanel.gameObject.activeSelf) return;
        if (playerInputField.gameObject.activeSelf || choicesContainer.childCount > 0) return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
        {
            DisplayNextLine();
        }
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.gameObject.SetActive(true);
        gamePanel.gameObject.SetActive(false);

        if (voiceSource != null) voiceSource.Stop();

        if (!string.IsNullOrEmpty(mainMenuBgmName) && currentBgmName != mainMenuBgmName)
        {
            AudioClip menuBgmClip = Resources.Load<AudioClip>("Audio/BGM/" + mainMenuBgmName);
            if (menuBgmClip != null && bgmSource != null)
            {
                bgmSource.clip = menuBgmClip;
                bgmSource.loop = true;
                bgmSource.Play();
                currentBgmName = mainMenuBgmName;
            }
        }
        else if (string.IsNullOrEmpty(mainMenuBgmName))
        {
            if (bgmSource != null) bgmSource.Stop();
            currentBgmName = "";
        }
    }

    public void StartGame()
    {
        if (!isGameReady) return; // Jangan mulai game kalau masih ada error

        mainMenuPanel.gameObject.SetActive(false);
        gamePanel.gameObject.SetActive(true);

        playerVars.Clear();
        visitedNodes.Clear();
        playerVars["checkloopone"] = 3;

        PlayNode("HeartCake: Start loop");
    }

    public void PlayNode(string nodeID)
    {
        if (storyDictionary.ContainsKey(nodeID))
        {
            currentNode = storyDictionary[nodeID];
            currentLineIndex = 0;

            if (currentNode.trigger_reset)
            {
                playerVars.Clear();
                visitedNodes.Clear();
                ShowMainMenu();
                return;
            }

            if (!string.IsNullOrEmpty(currentNode.bgm) && currentNode.bgm != currentBgmName)
            {
                AudioClip newBgm = Resources.Load<AudioClip>("Audio/BGM/" + currentNode.bgm);
                if (newBgm != null && bgmSource != null)
                {
                    bgmSource.clip = newBgm;
                    bgmSource.loop = true;
                    bgmSource.Play();
                    currentBgmName = currentNode.bgm;
                }
            }

            if (!string.IsNullOrEmpty(currentNode.background))
            {
                string cleanBgName = System.IO.Path.GetFileNameWithoutExtension(currentNode.background);
                string imagePath = "Sprites/Backgrounds/" + cleanBgName;
                Texture2D bgTex = Resources.Load<Texture2D>(imagePath);

                if (bgTex != null && backgroundImage != null)
                {
                    backgroundImage.texture = bgTex;
                    backgroundImage.color = Color.white;
                }
            }

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
        else
        {
            if (dialogueText != null) dialogueText.text = "ERROR: Node ID [" + nodeID + "] tidak ditemukan!";
        }
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
            if (speakerNameText != null) speakerNameText.text = line.speaker;
            if (dialogueText != null) dialogueText.text = line.text;

            if (voiceSource != null) voiceSource.Stop();

            if (!string.IsNullOrEmpty(line.voice_clip))
            {
                AudioClip voClip = Resources.Load<AudioClip>("Audio/Voices/" + line.voice_clip);
                if (voClip != null && voiceSource != null)
                {
                    voiceSource.clip = voClip;
                    voiceSource.loop = false;
                    voiceSource.Play();
                }
            }

            string spriteName = line.expression;
            Sprite charImg = Resources.Load<Sprite>("Sprites/Characters/" + spriteName);

            if (charImg != null && characterSprite != null)
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
            else if (characterSprite != null)
            {
                characterSprite.gameObject.SetActive(false);
            }

            currentLineIndex++;
        }
        else
        {
            if (currentNode.requires_input && playerInputField != null)
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
        if (currentNode.requires_input && playerInputField != null && playerInputField.gameObject.activeSelf)
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