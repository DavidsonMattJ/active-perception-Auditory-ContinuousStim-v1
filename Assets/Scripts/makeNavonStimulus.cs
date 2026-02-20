// ═══════════════════════════════════════════════════════════════════
// DISABLED: This Navon visual stimulus script has been replaced by
// MakeAuditoryStimulus.cs for the auditory duration discrimination task.
// Wrapped in #if false to prevent compile errors from removed enums.
// Remove the component from the StimulusScreen GameObject in the Inspector.
// ═══════════════════════════════════════════════════════════════════
#if false
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class makeNavonStimulus : MonoBehaviour
{
    /// <summary>
    /// script to procedurally generate a Navon stimulus in a  patch, 
    /// to be shown on the face of the screen object.
    /// </summary>

    int width = 1024;
    int height = 1024;
    public float offsetX, offsetY;
    Renderer rend;

    Texture2D currentTexture, nextTexture, maskTexture, fixationTexture;
    experimentParameters experimentParameters;
    public struct NavonParams
    {
        public float maskScale, px, py;
        public experimentParameters.StimulusType stimulusType;
        public experimentParameters.DetectionTask currentTask;
        public char globalLetter;
        public char localLetter;
        public bool targetPresent;   // Is the target letter (E or T) present?
        public bool isCongruent;     // Are global and local the same?
        public string trialCategory; // "Active" or "Inactive"
        public float targDuration;
    }

    public NavonParams navonP;

    // Letter patterns for all 4 letters: E, T, F, I
    private int[,] letterE = new int[,] {
        {1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0},
        {1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0},
        {1,1,1,1,1,1,1}
    };

    private int[,] letterT = new int[,] {
        {1,1,1,1,1,1,1},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0}
    };

    private int[,] letterF = new int[,] {
        {1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0},
        {1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0},
        {1,0,0,0,0,0,0}
    };

    private int[,] letterI = new int[,] {
        {1,1,1,1,1,1,1},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {0,0,0,1,0,0,0},
        {1,1,1,1,1,1,1}
    };

    // Hash mark pattern for masking - THIN VERSION (1 pixel lines)
    private int[,] hashMark = new int[,] {
        {0,1,0,0,0,1,0},  // Two thin vertical bars
        {0,1,0,0,0,1,0},
        {1,1,1,1,1,1,1},  // First horizontal bar
        {0,1,0,0,0,1,0},
        {1,1,1,1,1,1,1},  // Second horizontal bar
        {0,1,0,0,0,1,0},
        {0,1,0,0,0,1,0}   // Two thin vertical bars
    };

    [SerializeField]
    GameObject scriptHolder;
    void Start()
    {
        rend = GetComponent<Renderer>();
        experimentParameters = scriptHolder.GetComponent<experimentParameters>();
        offsetX = Random.Range(100f, 200f);
        offsetY = Random.Range(100f, 200f);

        // Default params - CENTERED ONLY
        navonP.maskScale = 60f;
        navonP.px = 0.5f;  // Always centered
        navonP.py = 0.5f;  // Always centered
        navonP.stimulusType = experimentParameters.StimulusType.E_BigE_LittleE;
        navonP.currentTask = experimentParameters.DetectionTask.DetectE;
        navonP.targetPresent = true;
        navonP.globalLetter = 'E';
        navonP.localLetter = 'E';
        navonP.isCongruent = true;
        navonP.trialCategory = "Active";
        navonP.targDuration =experimentParameters.targDurationsec;

        currentTexture = new Texture2D(width, height);
        for (int ix = 0; ix < width; ix++)
        {
            for (int iy = 0; iy < height; iy++)
            {
                currentTexture.SetPixel(ix, iy, Color.white);
            }
        }
        currentTexture.Apply();

        fixationTexture = GenerateFixationCross();
        nextTexture = GenerateNavon();
        showNavon();
        maskTexture = GenerateMask();
        
        Debug.Log("makeNavonStimulus initialized - DUAL DETECTION (E and T) with 12 stimulus types");
    }

    public void showNavon()
    {
        rend.material.mainTexture = nextTexture;
    }

    public void hideNavon()
    {
        rend.material.mainTexture = fixationTexture;
    }

    public void backwardMask()
    {
        rend.material.mainTexture = maskTexture;
        maskTexture = GenerateMask();
    }

    public Texture2D GenerateNavon()
    {
        Debug.Log($"=== GenerateNavon CALLED - Task: Detect {navonP.currentTask} ===");
        
        nextTexture = new Texture2D(width, height);

        // ALWAYS CENTERED
        navonP.px = 0.5f;
        navonP.py = 0.5f;

        // Generate stimulus based on current detection task
        if (navonP.currentTask == experimentParameters.DetectionTask.DetectE)
        {
            GenerateStimulusForE();
        }
        else  // DetectT
        {
            GenerateStimulusForT();
        }

        int[,] globalPattern = GetLetterPattern(navonP.globalLetter);
        int[,] localPattern = GetLetterPattern(navonP.localLetter);

        // Calculate center position (ALWAYS CENTERED)
        float cx = navonP.px * width;
        float cy = navonP.py * height;

        // FIXED SIZES
        int globalLetterSize = 400;
        int localLetterSize = 50;
        int spacing = 10;

        // Calculate effective size including spacing
        int effectiveGlobalSize = globalLetterSize + (spacing * 2);
        
        // Calculate starting position
        float startX = cx - (effectiveGlobalSize / 2f);
        float startY = cy - (effectiveGlobalSize / 2f);

        // Random offset for noise
        offsetX = Random.Range(100f, 200f);
        offsetY = Random.Range(100f, 200f);

        // White background
        for (int ix = 0; ix < width; ix++)
        {
            for (int iy = 0; iy < height; iy++)
            {
                nextTexture.SetPixel(ix, iy, Color.white);
            }
        }

        // Draw fixation cross
        DrawFixationCross(nextTexture);

        // Create the stimulus
        for (int ix = 0; ix < width; ix++)
        {
            for (int iy = 0; iy < height; iy++)
            {
                float pixelValue = 1.0f;

                float cellWidth = effectiveGlobalSize / 7f;
                float cellHeight = effectiveGlobalSize / 7f;
                
                int globalRow = (int)((iy - startY) / cellHeight);
                int globalCol = (int)((ix - startX) / cellWidth);

                if (globalRow >= 0 && globalRow < 7 && globalCol >= 0 && globalCol < 7)
                {
                    int flippedRow = 6 - globalRow;
                    
                    if (globalPattern[flippedRow, globalCol] == 1)
                    {
                        float cellStartX = startX + globalCol * (effectiveGlobalSize / 7f);
                        float cellStartY = startY + globalRow * (effectiveGlobalSize / 7f);
                        
                        cellStartX += spacing / 2f;
                        cellStartY += spacing / 2f;
                        
                        int localRow = (int)((iy - cellStartY) / (localLetterSize / 7f));
                        int localCol = (int)((ix - cellStartX) / (localLetterSize / 7f));

                        if (localRow >= 0 && localRow < 7 && localCol >= 0 && localCol < 7)
                        {
                            int flippedLocalRow = 6 - localRow;
                            
                            if (localPattern[flippedLocalRow, localCol] == 1)
                            {
                                pixelValue = 0.0f;
                            }
                        }
                    }
                }

                if (pixelValue < 0.99f)
                {
                    Color color = new Color(pixelValue, pixelValue, pixelValue);
                    nextTexture.SetPixel(ix, iy, color);
                }
            }
        }

        nextTexture.Apply();
        
        string presenceText = navonP.targetPresent ? "TARGET PRESENT" : "TARGET ABSENT";
        Debug.Log($"TEXTURE GENERATED: {navonP.trialCategory} - {presenceText} ({navonP.stimulusType})");
        Debug.Log($"  Global: {navonP.globalLetter}, Local: {navonP.localLetter}, Congruent: {navonP.isCongruent}");
        
        return nextTexture;
    }

    private void GenerateStimulusForE()
    {
        // 50% chance of active vs inactive
        bool showActive = Random.Range(0f, 1f) < 0.5f;
        
        if (showActive)
        {
            // ACTIVE TRIALS (E present - say YES)
            navonP.trialCategory = "Active";
            navonP.targetPresent = true;
            
            int activeType = Random.Range(0, 3);
            
            switch (activeType)
            {
                case 0:  // Big E, Little E (Congruent)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigE_LittleE;
                    navonP.globalLetter = 'E';
                    navonP.localLetter = 'E';
                    navonP.isCongruent = true;
                    break;
                case 1:  // Big E, Little I (Global-only, incongruent)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigE_LittleI;
                    navonP.globalLetter = 'E';
                    navonP.localLetter = 'I';
                    navonP.isCongruent = false;
                    break;
                case 2:  // Big I, Little E (Local-only, incongruent)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigI_LittleE;
                    navonP.globalLetter = 'I';
                    navonP.localLetter = 'E';
                    navonP.isCongruent = false;
                    break;
            }
        }
        else
        {
            // INACTIVE TRIALS (E absent - say NO)
            navonP.trialCategory = "Inactive";
            navonP.targetPresent = false;
            
            int inactiveType = Random.Range(0, 3);
            
            switch (inactiveType)
            {
                case 0:  // Big F, Little F (Congruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigF_LittleF;
                    navonP.globalLetter = 'F';
                    navonP.localLetter = 'F';
                    navonP.isCongruent = true;
                    break;
                case 1:  // Big T, Little F (Incongruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigT_LittleF;
                    navonP.globalLetter = 'T';
                    navonP.localLetter = 'F';
                    navonP.isCongruent = false;
                    break;
                case 2:  // Big F, Little T (Incongruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.E_BigF_LittleT;
                    navonP.globalLetter = 'F';
                    navonP.localLetter = 'T';
                    navonP.isCongruent = false;
                    break;
            }
        }
    }

    private void GenerateStimulusForT()
    {
        // 50% chance of active vs inactive
        bool showActive = Random.Range(0f, 1f) < 0.5f;
        
        if (showActive)
        {
            // ACTIVE TRIALS (T present - say YES)
            navonP.trialCategory = "Active";
            navonP.targetPresent = true;
            
            int activeType = Random.Range(0, 3);
            
            switch (activeType)
            {
                case 0:  // Big T, Little T (Congruent)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigT_LittleT;
                    navonP.globalLetter = 'T';
                    navonP.localLetter = 'T';
                    navonP.isCongruent = true;
                    break;
                case 1:  // Big T, Little E (Global-only, incongruent)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigT_LittleE;
                    navonP.globalLetter = 'T';
                    navonP.localLetter = 'E';
                    navonP.isCongruent = false;
                    break;
                case 2:  // Big E, Little T (Local-only, incongruent)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigE_LittleT;
                    navonP.globalLetter = 'E';
                    navonP.localLetter = 'T';
                    navonP.isCongruent = false;
                    break;
            }
        }
        else
        {
            // INACTIVE TRIALS (T absent - say NO)
            navonP.trialCategory = "Inactive";
            navonP.targetPresent = false;
            
            int inactiveType = Random.Range(0, 3);
            
            switch (inactiveType)
            {
                case 0:  // Big F, Little F (Congruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigF_LittleF;
                    navonP.globalLetter = 'F';
                    navonP.localLetter = 'F';
                    navonP.isCongruent = true;
                    break;
                case 1:  // Big I, Little F (Incongruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigI_LittleF;
                    navonP.globalLetter = 'I';
                    navonP.localLetter = 'F';
                    navonP.isCongruent = false;
                    break;
                case 2:  // Big F, Little I (Incongruent foil)
                    navonP.stimulusType = experimentParameters.StimulusType.T_BigF_LittleI;
                    navonP.globalLetter = 'F';
                    navonP.localLetter = 'I';
                    navonP.isCongruent = false;
                    break;
            }
        }
    }

    private int[,] GetLetterPattern(char letter)
    {
        switch (letter)
        {
            case 'E': return letterE;
            case 'T': return letterT;
            case 'F': return letterF;
            case 'I': return letterI;
            default:
                Debug.LogWarning($"Unknown letter: {letter}, defaulting to E");
                return letterE;
        }
    }

    Texture2D GenerateFixationCross()
    {
        fixationTexture = new Texture2D(width, height);

        for (int ix = 0; ix < width; ix++)
        {
            for (int iy = 0; iy < height; iy++)
            {
                fixationTexture.SetPixel(ix, iy, Color.white);
            }
        }

        DrawFixationCross(fixationTexture);
        fixationTexture.Apply();
        return fixationTexture;
    }

    void DrawFixationCross(Texture2D texture)
    {
        int centerX = width / 2;
        int centerY = height / 2;
        int crossSize = 10;
        int crossThickness = 2;

        // Horizontal line
        for (int x = centerX - crossSize; x <= centerX + crossSize; x++)
        {
            for (int t = 0; t < crossThickness; t++)
            {
                if (x >= 0 && x < width && centerY + t >= 0 && centerY + t < height)
                {
                    texture.SetPixel(x, centerY + t - crossThickness / 2, Color.black);
                }
            }
        }

        // Vertical line
        for (int y = centerY - crossSize; y <= centerY + crossSize; y++)
        {
            for (int t = 0; t < crossThickness; t++)
            {
                if (centerX + t >= 0 && centerX + t < width && y >= 0 && y < height)
                {
                    texture.SetPixel(centerX + t - crossThickness / 2, y, Color.black);
                }
            }
        }
    }

    Texture2D GenerateMask()
    {
        maskTexture = new Texture2D(width, height);

        // White background
        for (int ix = 0; ix < width; ix++)
        {
            for (int iy = 0; iy < height; iy++)
            {
                maskTexture.SetPixel(ix, iy, Color.white);
            }
        }

        // Create 7x7 grid of hash marks - SLIGHTLY BIGGER THAN STIMULUS
        int gridSize = 7;  // 7x7 grid
        int hashSize = 55; // Slightly bigger hash marks (was 50)
        int spacing = 12;  // Slightly more spacing (was 10)
        
        // Total grid: 7 × (55 + 12) - 12 = 457 pixels (covers 400px stimulus nicely)
        int totalGridWidth = (hashSize + spacing) * gridSize - spacing;
        int totalGridHeight = (hashSize + spacing) * gridSize - spacing;
        
        // Center the grid
        int startX = (width - totalGridWidth) / 2;
        int startY = (height - totalGridHeight) / 2;

        // Draw each hash mark in the grid
        for (int gridRow = 0; gridRow < gridSize; gridRow++)
        {
            for (int gridCol = 0; gridCol < gridSize; gridCol++)
            {
                int hashStartX = startX + gridCol * (hashSize + spacing);
                int hashStartY = startY + gridRow * (hashSize + spacing);
                
                DrawHashMark(maskTexture, hashStartX, hashStartY, hashSize);
            }
        }

        maskTexture.Apply();
        return maskTexture;
    }

    void DrawHashMark(Texture2D texture, int startX, int startY, int size)
    {
        // Draw solid hash mark with continuous lines - THICKER VERSION
        int lineThickness = 5;  // 5 pixels thick for better visibility
        
        // Calculate positions for the hash mark
        int verticalBar1 = size / 3;      // Left vertical bar position
        int verticalBar2 = (size * 2) / 3; // Right vertical bar position
        int horizontalBar1 = size / 3;     // Top horizontal bar position
        int horizontalBar2 = (size * 2) / 3; // Bottom horizontal bar position
        
        // Draw two VERTICAL bars (solid lines)
        for (int y = 0; y < size; y++)
        {
            // Left vertical bar
            for (int thickness = 0; thickness < lineThickness; thickness++)
            {
                int pixelX = startX + verticalBar1 + thickness;
                int pixelY = startY + y;
                if (pixelX >= 0 && pixelX < width && pixelY >= 0 && pixelY < height)
                {
                    texture.SetPixel(pixelX, pixelY, Color.black);
                }
            }
            
            // Right vertical bar
            for (int thickness = 0; thickness < lineThickness; thickness++)
            {
                int pixelX = startX + verticalBar2 + thickness;
                int pixelY = startY + y;
                if (pixelX >= 0 && pixelX < width && pixelY >= 0 && pixelY < height)
                {
                    texture.SetPixel(pixelX, pixelY, Color.black);
                }
            }
        }
        
        // Draw two HORIZONTAL bars (solid lines)
        for (int x = 0; x < size; x++)
        {
            // Top horizontal bar
            for (int thickness = 0; thickness < lineThickness; thickness++)
            {
                int pixelX = startX + x;
                int pixelY = startY + horizontalBar1 + thickness;
                if (pixelX >= 0 && pixelX < width && pixelY >= 0 && pixelY < height)
                {
                    texture.SetPixel(pixelX, pixelY, Color.black);
                }
            }
            
            // Bottom horizontal bar
            for (int thickness = 0; thickness < lineThickness; thickness++)
            {
                int pixelX = startX + x;
                int pixelY = startY + horizontalBar2 + thickness;
                if (pixelX >= 0 && pixelX < width && pixelY >= 0 && pixelY < height)
                {
                    texture.SetPixel(pixelX, pixelY, Color.black);
                }
            }
        }
    }


}
#endif