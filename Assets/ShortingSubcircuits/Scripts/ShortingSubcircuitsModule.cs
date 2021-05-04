using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace ShortingSubcircuits {
    public class ShortingSubcircuitsModule : MonoBehaviour {
        // Constants
        private const string MODULE_NAME = "Shorting Subcircuits";
        private const int NUM_NODES = 12;
        private const float HOLD_TIME = 0.4f;
        private static readonly Color LED_OFF_COLOR = new Color(0.28f, 0f, 0f);
        private static readonly Color LED_ON_COLOR = Color.red;

        // Module ID
        private static int _nextId = 0;
        private int _moduleId;

        // KT hooks
        public KMBombModule bombModule;
        public KMAudio bombAudio;

        // Inspector hooks
        public KMSelectable[] nodes;
        public MeshRenderer[] LEDMeshes;
        public Light[] LEDLights;

        // State
        private Puzzle _puzzle;
        private int _pressedButton = -1;
        private float _pressStartTime = 0f;
        private bool _holdInProgress = false;
        private readonly bool[] _ledStates = new bool[NUM_NODES];
        private bool _inputAllowed = false;

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
            
            // Generate puzzle
            this.GeneratePuzzle();
            
            // Clear LEDs
            this.ClearLEDs();
            this.UpdateLEDs();
        }

        private void Update() {
            if (!this._holdInProgress && this._pressedButton != -1 && Time.time - this._pressStartTime > HOLD_TIME) {
                this._holdInProgress = true;
                
                this.HandleButtonHold(this._pressedButton);
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
            var shortOrder = this._puzzle.GetShortOrder();
            for (int i = 0; i < shortOrder.Count; i++) {
                shortOrder[i].Sort((x, y) => x.First.CompareTo(y.First));
                foreach (var connection in shortOrder[i]) {
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

            for (int i = 0; i < 3; i++) {
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
            
            this._pressedButton = button;
            this._pressStartTime = Time.time;
        }

        private void HandleRelease(int button) {
            if (!this._inputAllowed) {
                return;
            }
            
            float heldTime = Time.time - this._pressStartTime;

            if (heldTime < HOLD_TIME) {
                this.HandleButtonPress(button);
            } else {
                this.HandleButtonRelease(button);
            }

            this._pressedButton = -1;
            this._pressStartTime = 0f;
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
                this.LEDMeshes[i].material.color = this._ledStates[i] ? LED_ON_COLOR : LED_OFF_COLOR;
            }
        }
        
        
        private void HandleButtonPress(int button) {
            this.Log("Button {0} pressed!", button);
            this.bombModule.HandleStrike();
        }

        private void HandleButtonHold(int button) {
            this.Log("Button {0} held!", button);
            
            // Change LED state
            this.ShowConnections(button);
            
            // Update LED display
            this.UpdateLEDs();
        }

        private void HandleButtonRelease(int button) {
            this.Log("Button {0} released!", button);
            this._holdInProgress = false;

            // Clear LED state
            this.ClearLEDs();
            
            // Update LED display
            this.UpdateLEDs();
        }
    }
}
