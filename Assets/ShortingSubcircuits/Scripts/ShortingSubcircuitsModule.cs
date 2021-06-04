using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using ShortingSubcircuits;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShortingSubcircuitsModule : MonoBehaviour {
    // Constants
    private const string MODULE_NAME = "Shorting Subcircuits";
    private const int NUM_NODES = 12;
    private const float HOLD_TIME = 0.35f;
    private const int MODULE_VERSION = 1;
    private static readonly string[] PCB_TYPES_DEFAULT = {"STK", "TMR", "PCG", "ZKZ"};

    private static readonly Pair<string, Predicate<KMBombInfo>>[] PCB_TYPES_DEPENDENT = {
        new Pair<string, Predicate<KMBombInfo>>("2FA", info => info.GetTwoFactorCounts() > 0),
        new Pair<string, Predicate<KMBombInfo>>("DSP", info => info.GetPortCount(Port.DVI) > 0),
        new Pair<string, Predicate<KMBombInfo>>("AUD", info => info.GetPortCount(Port.StereoRCA) > 0),
        new Pair<string, Predicate<KMBombInfo>>("NET", info => info.GetPortCount(Port.RJ45) > 0),
        new Pair<string, Predicate<KMBombInfo>>("COM",
            info => info.GetPortCount(Port.Parallel) + info.GetPortCount(Port.Serial) > 0),
        new Pair<string, Predicate<KMBombInfo>>("PER", info => info.GetPortCount(Port.PS2) > 0),
        new Pair<string, Predicate<KMBombInfo>>("BTY", info => info.GetBatteryCount() > 0),
        new Pair<string, Predicate<KMBombInfo>>("IND", info => info.GetIndicators().Any()),
        new Pair<string, Predicate<KMBombInfo>>("BOB", info => info.GetIndicators().Contains("BOB")),
    };

    // Module ID
    private static int _nextId = 0;
    private int _moduleId;

    // KT hooks
    public KMBombModule bombModule;
    public KMAudio bombAudio;
    public KMBombInfo bombInfo;

    // Inspector hooks
    public KMSelectable[] nodes;
    public MeshRenderer[] LEDMeshes;
    public Light[] LEDLights;
    public Material LEDOnMaterial;
    public Material LEDOffMaterial;
    public TextMesh PCBText;

    // State
    private Puzzle _puzzle;
    private List<List<Pair<int, int>>> _solution;

    private bool _inputAllowed = false;

    private int _heldButton = -1;
    private float _holdStartTime = 0f;
    private bool _holdInProgress = false;

    private int _firstSelectedButton = -1;

    private readonly bool[] _ledStates = new bool[NUM_NODES];
    private Coroutine _flashingCoroutine;

    private void Start() {
        // Module ID
        this._moduleId = ++_nextId;

        // Hook up delegates for buttons
        for (int index = 0; index < this.nodes.Length; index++) {
            int button = index;
            this.nodes[index].OnInteract += delegate {
                this.HandlePress(button);
                return false;
            };
            this.nodes[index].OnInteractEnded += delegate { this.HandleRelease(button); };
        }

        // Hook up bomb events
        this.bombModule.OnActivate += this.OnActivate;

        // Generate the PCB info text
        this.GeneratePCBInfo();

        // Generate puzzle
        this.GeneratePuzzle();

        // Clear LEDs
        this.ClearLEDs();
        this.UpdateLEDs();

        // Adjust LED range
        float scalar = this.transform.lossyScale.x;
        foreach (Light l in this.LEDLights) {
            l.range *= scalar;
        }
    }

    private void GeneratePCBInfo() {
        // Random values
        string pcbType = PCB_TYPES_DEFAULT.Concat(PCB_TYPES_DEPENDENT
            .Where(p => p.Second.Invoke(this.bombInfo)).Select(p => p.First)).PickRandom();
        int id1 = Random.Range(0, 65536);
        int id2 = Random.Range(0, 65536);
        int id3 = Random.Range(0, 65536);

        // Text
        string pcbInfo = string.Format("KTANE_{0}_{1:D2}\nMODEL# SS{2:D4}\n{3:X4}-{4:X4}-{5:X4}", pcbType,
            MODULE_VERSION, this._moduleId, id1, id2, id3);

        // PCB text
        this.PCBText.text = pcbInfo;

        // Log
        this.Log("PCB: {0}", pcbInfo.Replace("\n", " / "));
    }

    private void Update() {
        if (!this._holdInProgress && this._heldButton != -1 && Time.time - this._holdStartTime > HOLD_TIME) {
            this._holdInProgress = true;

            this.HandleButtonHold(this._heldButton);
        }
    }

    private void GeneratePuzzle() {
        // Generate puzzle
        this.Log("Beginning puzzle generation.");
        this._puzzle = PuzzleGenerator.GetDefaultPuzzleGenerator(NUM_NODES).GeneratePuzzle();

        // Log puzzle
        this.Log("Puzzle generation complete.");
        this.Log("Graph:");
        for (int i = 0; i < NUM_NODES; i++) {
            this._puzzle.GetConnections(i).Sort();
            this.Log("   {0} -> {1}", i,
                string.Join(", ", this._puzzle.GetConnections(i).Select(x => x.ToString()).ToArray()));
        }

        // Log components
        this.Log("Components:");
        this._puzzle.GetComponents().Peek((comp, i) => {
            comp.Sort();
            this.Log("   {0}: {1}", i, string.Join(", ", comp.Select(x => x.ToString()).ToArray()));
        });

        // Log solution
        this.Log("Intended solution:");
        this._solution = this._puzzle.GetShortOrder();
        for (int i = 0; i < this._solution.Count; i++) {
            this._solution[i].Sort((x, y) => x.First.CompareTo(y.First));
            foreach (var connection in this._solution[i]) {
                this.Log("   {0}: {1} -> {2}", i, connection.First, connection.Second);
            }
        }
    }

    private void OnActivate() {
        this.StartCoroutine(this.StartupAnimation());
    }

    private IEnumerator StartupAnimation() {
        yield return new WaitForSeconds(0.3f);

        for (int i = 0; i < NUM_NODES; i++) {
            this._ledStates[i] = true;
            this.UpdateLEDs();

            yield return new WaitForSeconds(0.3f);
        }

        for (int i = 0; i < 2; i++) {
            this.ClearLEDs();
            this.UpdateLEDs();
            yield return new WaitForSeconds(0.5f);

            this.LightAllLEDs();
            this.UpdateLEDs();
            yield return new WaitForSeconds(0.5f);
        }

        this.ClearLEDs();
        this.UpdateLEDs();

        this._inputAllowed = true;
    }

    private void HandlePress(int button) {
        if (!this._inputAllowed) {
            return;
        }

        this._heldButton = button;
        this._holdStartTime = Time.time;
    }

    private void HandleRelease(int button) {
        if (!this._inputAllowed || this._heldButton == -1) {
            return;
        }

        float heldTime = Time.time - this._holdStartTime;

        if (heldTime < HOLD_TIME) {
            this.HandleButtonPress(button);
        } else {
            this.HandleButtonRelease(button);
        }

        this._heldButton = -1;
        this._holdStartTime = 0f;
    }

    private void Log(string format, params object[] args) {
        Debug.LogFormat(string.Format("[{0} #{1}] ", MODULE_NAME, this._moduleId) + format, args);
    }

    private void ShowConnections(int pressedButton) {
        // Clear LEDs
        this.ClearLEDs();

        // Light up connections
        foreach (int connection in this._puzzle.GetConnections(pressedButton)) {
            this._ledStates[connection] = true;
        }
    }

    private void ClearLEDs() {
        for (int i = 0; i < NUM_NODES; i++) {
            this._ledStates[i] = false;
        }
    }

    private void LightAllLEDs() {
        for (int i = 0; i < NUM_NODES; i++) {
            this._ledStates[i] = true;
        }
    }

    private void UpdateLEDs() {
        // for LED in LEDs: update color
        for (int i = 0; i < NUM_NODES; i++) {
            this.LEDMeshes[i].material = this._ledStates[i] ? this.LEDOnMaterial : this.LEDOffMaterial;
            this.LEDLights[i].enabled = this._ledStates[i];
        }
    }


    private void HandleButtonPress(int button) {
        this.Log("Button {0} pressed!", button);
        if (this._firstSelectedButton == -1) {
            this.HandleFirstSelection(button);
        } else if (button == this._firstSelectedButton) {
            this.HandleCancelFirstSelection();
        } else {
            this.HandleSecondSelection(button);
        }
    }

    private void HandleFirstSelection(int button) {
        this.Log("First button pressed!");
        
        // Start flashing
        this._flashingCoroutine = this.StartCoroutine(this.DoubleFlashLED(button));

        this._firstSelectedButton = button;
    }

    private void HandleCancelFirstSelection() {
        this._firstSelectedButton = -1;
        this.StopCoroutine(this._flashingCoroutine);
    }

    private void HandleSecondSelection(int button) { }

    private void HandleButtonHold(int button) {
        this.Log("Button {0} held!", button);

        // Change LED state
        this.ShowConnections(button);

        // Update LED display
        this.UpdateLEDs();

        // Start flashing
        this._flashingCoroutine = this.StartCoroutine(this.FlashLED(this._heldButton));
    }

    private IEnumerator FlashLED(int num) {
        while (true) {
            yield return new WaitForSeconds(0.5f);
            this._ledStates[num] ^= true;
            this.UpdateLEDs();
        }
    }

    private void HandleButtonRelease(int button) {
        this.Log("Button {0} released!", button);
        this._holdInProgress = false;

        // Clear LED state
        this.ClearLEDs();

        // Update LED display
        this.UpdateLEDs();

        // Stop flashing coroutine
        this.StopCoroutine(this._flashingCoroutine);
        this._flashingCoroutine = null;
    }


    private IEnumerator DoubleFlashLED(int num) {
        while (true) {
            yield return new WaitForSeconds(0.75f);
            this._ledStates[num] = true;
            this.UpdateLEDs();
            yield return new WaitForSeconds(0.1f);
            this._ledStates[num] = false;
            this.UpdateLEDs();
            yield return new WaitForSeconds(0.2f);
            this._ledStates[num] = true;
            this.UpdateLEDs();
            yield return new WaitForSeconds(0.1f);
            this._ledStates[num] = false;
            this.UpdateLEDs();
        }
    }
}
