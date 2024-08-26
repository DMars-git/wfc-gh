using System;
using System.Drawing;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using Grasshopper.Kernel;

namespace gh_WFC_lib
{
    public class gh_WFC_libInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "ghWFClib";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "Contains classes for using wave function collapse in Grasshopper";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("473d0a88-8d6d-4f2b-9954-3b20fdd7cc97");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "HP Inc.";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "";
            }
        }
    }
    public class Module //a module which represents a possible collapsed state on the grid. Contains geometry to instantiate when the Grid is as collapsed as possible
    {
        public string TypeName { get; set; }
        public List<GeometryBase> Geom { get; set; }
        public Point3d refOrigin { get; set; }
        //public int Orientation { get; set; } //an integer 0-3 to represent rotation (0 being the original orientation, and 1-3 being 90, 180, and 270 degrees clockwise

        public Module() { }
        public Module(string n, List<GeometryBase> g, int o, Point3d ro)
        {
            TypeName = n;
            Geom = g;
            //Orientation = o;
            refOrigin = ro;
        }
    }
    public class Cell3d //represents a coordinate location on a grid, and contains a list of possible tiles it could be collapsed into
    {
        public List<Module> PModules { get; set; }
        public int Entropy { get { return PModules.Count; } }
        public Module CModule { get; set; }
        public Coord Coord { get; set; }
        public bool IsCollapsed { get; set; }
        public bool Uncollapsable { get; set; }
        public Grid3d Grid { get; set; }
        public Cell3d(int _x, int _y, int _z, List<Module> _pModules, Grid3d _g) //use this constructor if you know the coords and pTiles ahead of time
        {
            Coord = new Coord(_x, _y, _z);
            PModules = _pModules;
            IsCollapsed = false;
            Uncollapsable = false;
            Grid = _g;
            Grid.UsableCells.Add(this);
        }
        public void Collapse(Random rnd) //choose a random Tile from the list of potential Tiles
        {
            int select = rnd.Next(0, PModules.Count);
            //Grid.GHL.AddLine("COLLAPSE RANDOM INT = " + select + ", PMODULES COUNT = " + Entropy);
            CModule = PModules[select];
            Grid.GHL.AddLine("Collapsing cell " + Coord.X + "," + Coord.Y + "," + Coord.Z + ". It's properties are: ");
            Grid.GHL.AddLine("Ent = " + Entropy + "; CMod = " + CModule.TypeName + "; IsCol = " + IsCollapsed);
            Grid.UsableCells.Remove(this);
            PModules.Clear();
            //PModules.Add(CModule);
            IsCollapsed = true;
            Grid.SaveGridState(this);
        }
        public void RemovePModule(string name) //remove a Tile from the list of potential tiles. Use when propogating collapsed states
        {
            foreach (Module m in PModules)
            {
                if (m.TypeName == name)
                {
                    PModules.Remove(m);
                    break;
                }
            }
        }
    }
    public class GHLogger //use to log stuff separately from the grid
    {
        public List<string> GHLog { get; set; }
        public GHLogger()
        {
            GHLog = new List<string>();
        }
        public void AddLine(string s)
        {
            GHLog.Add(s);
        }
    }
    public class Grid3d //represents a rectangular volume grid of cells, that will be the collapsed into the final arrangement of modules
    {
        private Cell3d[,,] cGrid;
        public List<Module[,,]> gridStates;
        public List<Coord> cellCollapseOrder;
        public List<Cell3d> UsableCells;
        public int EX { get; set; } //X dimension of output grid
        public int EY { get; set; } //Y dimension of output grid
        public int EZ { get; set; } //Z dimension of output grid
        public int IX { get; set; } //X dimension of input grid
        public int IY { get; set; } //Y dimension of input grid
        public int IZ { get; set; } //Z dimension of input grid
        public int PS { get; set; } //pattern size
        public bool UnSolvable { get; set; } //FOR DELETION
        public Random R { get; set; } //use for any rng the grid uses
        public List<Module> AllModules { get; set; }
        public Module DefaultModule { get; set; } //the module to use if a cell is uncollapsable
        public Module[,,] InputMap { get; set; }
        public List<Pattern> AllPatterns { get; set; }
        public GHLogger GHL {get; set;}
        private bool AllowInputTilingX { get; set; }
        private bool AllowInputTilingY { get; set; }
        private bool AllowInputTilingZ { get; set; }
        private bool AllowOutputTilingX { get; set; }
        private bool AllowOutputTilingY { get; set; }
        private bool AllowOutputTilingZ { get; set; }
        private float EntropySelectionThreshold { get; set; }

        public Grid3d(int eX, int eY, int eZ, int iX, int iY, int iZ, List<Module> _allModules, Module _defMod, GHLogger _ghl) //constructor
        {
            //GHPrint = new List<string>();
            EX = eX;
            EY = eY;
            EZ = eZ;
            IX = iX;
            IY = iY;
            IZ = iZ;
            UnSolvable = false;
            AllModules = _allModules;
            DefaultModule = _defMod; //CELLS ASSIGNED THE DEFAULT MODULE SHOULD BE TREATED AS WILDCARDS
            GHL = _ghl;
            GHL.AddLine("Grasshopper Logger created");
            UsableCells = new List<Cell3d>();
            cGrid = InitCellGrid(EX, EY, EZ); //at this point, the Grid should have an array of uncollapsed Cells
            AllPatterns = new List<Pattern>(); //create empty list of patterns. This will be populated when Solve() is called
            InputMap = new Module[IX, IY, IZ];
            AllowInputTilingX = false;
            AllowInputTilingY = false;
            AllowInputTilingZ = false;
            AllowOutputTilingX = false;
            AllowOutputTilingY = false;
            AllowOutputTilingZ = false;
            EntropySelectionThreshold = 0.01f;
            gridStates = new List<Module[,,]>();
            cellCollapseOrder = new List<Coord>();
            GHL.AddLine("Grid constructor complete");
        }
        private Cell3d[,,] InitCellGrid(int eX, int eY, int eZ) //create a rectangular array of cells, with all tiles considered potential to start with
        {
            GHL.AddLine("Initializing Grid");
            Cell3d[,,] cA = new Cell3d[eX, eY, eZ];
            for (int x = 0; x < eX; x++)
            {
                for (int y = 0; y < eY; y++)
                {
                    for (int z = 0; z < eZ; z++)
                    {
                        cA[x, y, z] = new Cell3d(x, y, z, new List<Module>(AllModules), this);
                        //UncollapsedCells.Add(cA[x, y]);
                    }
                }
            }
            return cA;
        }
        public Cell3d GetCell(int x, int y, int z) //get any cell in the grid
        {
            return cGrid[x, y, z];
        }
        public Module GetCellModuleInState(int x, int y, int z, int state)
        {
            return gridStates[state][x, y, z];
        }
        public void SaveGridState(Cell3d c)
        {
            GHL.AddLine("...Saving grid state");
            Module[,,] ma = new Module[EX, EY, EZ];
            for (int x = 0; x < EX; x++)
            {
                for (int y = 0; y < EY; y++)
                {
                    for (int z = 0; z < EZ; z++)
                    {
                        if (cGrid[x, y, z].IsCollapsed)
                        {
                            ma[x, y, z] = cGrid[x, y, z].CModule;
                        }
                        else
                        {
                            ma[x, y, z] = DefaultModule;
                        }
                    }
                }
            }
            gridStates.Add(ma);
            cellCollapseOrder.Add(c.Coord);
            GHL.AddLine("...Grid state saved");
        }
        public Module[,,] Solve(string[,,] pat, int patternSize, bool allowInputTilingX, bool allowInputTilingY, bool allowInputTilingZ, bool allowOutputTilingX, bool allowOutputTilingY, bool allowOutputTilingZ, int attempts, int seed, float entSelThres) //attempts to perform wave function collapse on the current Grid
        {
            //create patterns from input board
            GHL.AddLine("Solving Grid. Output pattern is " + EX + "x" + EY + "x" + EZ + ". Input pattern is " + IX + "x" + IY + "x" + IZ + ". Pattern size is " + patternSize + "x" + patternSize);
            R = new Random(seed);
            foreach (Module module in AllModules)
            {
                GHL.AddLine("...Including module type: " + module.TypeName);
            }
            PS = patternSize;
            AllowInputTilingX = allowInputTilingX;
            AllowInputTilingY = allowInputTilingY;
            AllowInputTilingZ = allowInputTilingZ;
            AllowOutputTilingX = allowOutputTilingX;
            AllowOutputTilingY = allowOutputTilingY;
            AllowOutputTilingZ = allowOutputTilingZ;
            EntropySelectionThreshold = entSelThres;
            for (int x = 0; x < IX; x++)
            {
                for (int y = 0; y < IY; y++)
                {
                    for (int z = 0; z < IZ; z++)
                    {
                        InputMap[x, y, z] = GetModuleByTypeName(pat[x, y, z]);
                    }
                }
            }
            AllPatterns = GeneratePatterns(InputMap, patternSize);
            CullDupAllPatterns();
            //begin collapsing the output map
            WFCollapseGrid(attempts);
            //return an array of modules from the collapsed cell grid
            GHL.AddLine("Final Grid Output:");
            Module[,,] result = new Module[EX,EY,EZ];
            for (int x = 0; x < EX; x++)
            {
                for (int y = 0; y < EY; y++)
                {
                    for (int z = 0; z < EZ; z++)
                    {
                        if (cGrid[x, y, z].CModule != null)
                        {
                            result[x, y, z] = cGrid[x, y, z].CModule;
                            GHL.AddLine("... " + x + "," + y + "," + z + ": " + result[x, y, z].TypeName);
                        }
                        else
                        {
                            result[x, y, z] = DefaultModule;
                        }
                    }
                }
            }
            return result;
        }
        private void WFCollapseGrid(int attempts) //collapse all cells with wave function collapse
        {
            GHL.AddLine("Collapsing grid. There are " + UsableCells.Count + " uncollapsed cells. Attempts = " + attempts);
            int esc = 0;
            while (UsableCells.Count > 0 && esc < attempts)
            {
                GHL.AddLine("... Collapse/Propagate cycle #" + esc);
                if (!UnSolvable)
                {
                    if (UsableCells.Count > 0)
                    {
                        Cell3d c = PickWithLowestEntropy(UsableCells); //collapse a random cell with minimum (but not 0) entropy
                        if (c != null)
                        {
                            c.Collapse(R);
                            UsableCells.Remove(c); //PROBABLY CAN REMOVE THIS
                            Propagate(cGrid, c);
                        }
                    }
                    GHL.AddLine("..." + UsableCells.Count + " uncollapsed cells remaining");
                }
                else
                {
                    GHL.AddLine("... breaking Collapse/Propagation Cycle. Unsolvable");
                    break;
                }
                esc++;
            }
        }
        private Cell3d PickWithLowestEntropy(List<Cell3d> uCells) //picks a random cell with the lowest entropy
        {
            float lowestAvgEntropy = AllModules.Count;
            if (uCells.Count > 0)
            {
                foreach (Cell3d c in uCells)
                {
                    float cLocAvgEnt = LocalAverageEntropy(c);
                    if (lowestAvgEntropy < cLocAvgEnt && cLocAvgEnt > 0)
                    {
                        lowestAvgEntropy = cLocAvgEnt;
                    }
                }
                List<Cell3d> lowECells = new List<Cell3d>();
                foreach (Cell3d c in uCells)
                {
                    if (LocalAverageEntropy(c) - lowestAvgEntropy <= EntropySelectionThreshold)
                    {
                        lowECells.Add(c);
                    }
                }
                Cell3d r = lowECells[R.Next(0, lowECells.Count - 1)];
                return r;
            }
            return null;
        }
        private float LocalAverageEntropy(Cell3d c) //calculates the average entropy of all the cells nearby a cell
        {
            List<float> entropies = new List<float>();
            for (int x = -PS + 1; x < PS; x++)
            {
                for (int y = -PS + 1; y < PS; y++)
                {
                    for (int z = -PS + 1; z < PS; z++)
                    {
                        int cx = c.Coord.X + x;
                        int cy = c.Coord.Y + y;
                        int cz = c.Coord.Z + z;
                        if (AllowOutputTilingX)
                        {
                            cx = FindTiledCoord(cx, EX);
                        }
                        if (AllowOutputTilingY)
                        {
                            cy = FindTiledCoord(cy, EY);
                        }
                        if (AllowOutputTilingZ)
                        {
                            cz = FindTiledCoord(cz, EZ);
                        }
                        if (cx >= 0 && cx < EX && cy >= 0 && cy < EY && cz >= 0 && cz < EZ)
                        {
                            entropies.Add(cGrid[cx, cy, cz].Entropy);
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }
            return entropies.Average();
        }
        private void Propagate(Cell3d[,,] cGrid, Cell3d c)
        {
            GHL.AddLine("New propagation cycle starting from cell " + c.Coord.X + "," + c.Coord.Y + "," + c.Coord.Z);
            Queue<Cell3d> cQ = new Queue<Cell3d>();
            cQ.Enqueue(c);
            //List<Cell3d> cQ = new List<Cell3d>();
            //cQ.Add(c);
            while (cQ.Count > 0)
            {
                Cell3d cc = cQ.Dequeue();
                //int ri = R.Next(0, cQ.Count);
                //Cell3d cc = cQ[ri];
                //cQ.RemoveAt(ri);

                GHL.AddLine("...Next in propagation queue is cell " + cc.Coord.X + "," + cc.Coord.Y + "," + cc.Coord.Z);
                //create a dictionary array to store "cleared" flags for every pModule in every uncollapsed cell in this area
                Dictionary<string, bool>[,,] pModuleFlags = new Dictionary<string, bool>[(2 * PS) - 1, (2 * PS) - 1, (2 * PS) - 1];
                for (int i = -PS + 1; i < PS; i++)
                {
                    for (int j = -PS + 1; j < PS; j++)
                    {
                        for (int k = -PS + 1; k < PS; k++)
                        {
                            int dX = cc.Coord.X + i;
                            int dY = cc.Coord.Y + j;
                            int dZ = cc.Coord.Z + k;
                            pModuleFlags[i + (PS - 1), j + (PS - 1), k + (PS - 1)] = new Dictionary<string, bool>();
                            if (dX < EX && dX >= 0 && dY < EY && dY >= 0 && dZ < EZ && dZ >= 0) //first check if the targeted cell is in bounds
                            {
                                if (!cGrid[dX, dY, dZ].IsCollapsed) //if the cell is not collapsed, copy all its potential modules' names
                                {
                                    foreach (Module m in cGrid[dX, dY, dZ].PModules)
                                    {
                                        pModuleFlags[i + (PS - 1), j + (PS - 1), k + (PS - 1)].Add(m.TypeName, false);
                                    }
                                }
                            }
                        }
                    }
                }
                //test all patterns in all cells surrounding the propagating cell
                GHL.AddLine("...1. Testing patterns surrounding cell");
                for (int x = -PS + 1; x <= 0; x++) //overlaid pattern loop x
                {
                    for (int y = -PS + 1; y <= 0; y++) //overlaid pattern loop y
                    {
                        for (int z = -PS + 1; z <= 0; z++) //overlaid pattern loop z
                        {
                            //cc is the propagating cell
                            int cX = cc.Coord.X + x;
                            int cY = cc.Coord.Y + y;
                            int cZ = cc.Coord.Z + z;
                            List<Pattern> matchedPatterns = new List<Pattern>();
                            GHL.AddLine("....1a.Finding matching patterns for cell " + cX + "," + cY + "," + cZ);
                            //find all patterns matching all given cells in this area, and add them to matchedPatterns
                            foreach (Pattern p in AllPatterns)
                            {
                                for (int i = 0; i < PS; i++) //if this loop runs all the way through without hitting nextPattern:, the pattern is a possibility, so add it to matchedPatterns
                                {
                                    for (int j = 0; j < PS; j++)
                                    {
                                        for (int k = 0; k < PS; k++)
                                        {
                                            int dX = cX + i;
                                            int dY = cY + j;
                                            int dZ = cZ + k;
                                            if (AllowOutputTilingX)
                                            {
                                                dX = FindTiledCoord(dX, EX);
                                            }
                                            if (AllowOutputTilingY)
                                            {
                                                dY = FindTiledCoord(dY, EY);
                                            }
                                            if (AllowOutputTilingZ)
                                            {
                                                dZ = FindTiledCoord(dZ, EZ);
                                            }
                                            if (dX < EX && dX >= 0 && dY < EY && dY >= 0 && dZ < EZ && dZ >= 0)
                                            {
                                                if (!cGrid[dX, dY, dZ].Uncollapsable && cGrid[dX, dY, dZ].IsCollapsed && p.MPat3d[i, j, k].TypeName != cGrid[dX, dY, dZ].CModule.TypeName) //if cell is collapsed and doesn't match, it's an invalid pattern
                                                {
                                                    //GHL.AddLine("......Pattern " + AllPatterns.IndexOf(p) + " eliminated");
                                                    goto nextPattern;
                                                }
                                                else if (!cGrid[dX, dY, dZ].Uncollapsable && !cGrid[dX, dY, dZ].IsCollapsed) //if it's not collapsed and the cell in pModules doesn't contain the cell in the pattern, it's invalid
                                                {
                                                    if (!CheckForModuleTypeNameInList(p.MPat3d[i, j, k].TypeName, cGrid[dX, dY, dZ].PModules))
                                                    {
                                                        //GHL.AddLine("......Pattern " + AllPatterns.IndexOf(p) + " eliminated. MODULE NOT FOUND IN CELL PMODULES (B)");
                                                        goto nextPattern;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                //GHL.AddLine("......Pattern " + AllPatterns.IndexOf(p) + " is valid. Adding to matchedPatterns.");
                                matchedPatterns.Add(p);
                                nextPattern:;
                            }
                            //Check each pattern against each uncollapsed cell, and validate the remaining possiblities
                            GHL.AddLine("....1b. Validating remaining possible modules for cell "/* + cX + "," + cY + "," + cZ*/);
                            for (int i = 0; i < PS; i++) //pattern test loop x
                            {
                                for (int j = 0; j < PS; j++) //pattern test loop y
                                {
                                    for (int k = 0; k < PS; k++) //pattern test loop z
                                    {
                                        int pX = cX + i; //coords on the output grid
                                        int pY = cY + j;
                                        int pZ = cZ + k;
                                        if (AllowOutputTilingX)
                                        {
                                            pX = FindTiledCoord(pX, EX);
                                        }
                                        if (AllowOutputTilingY)
                                        {
                                            pY = FindTiledCoord(pY, EY);
                                        }
                                        if (AllowOutputTilingZ)
                                        {
                                            pZ = FindTiledCoord(pZ, EZ);
                                        }
                                        //GHL.AddLine("......... validating at " + pX + "," + pY + "," + pZ);
                                        int fX = (PS - 1) + x + i; //coords on the local flag map
                                        int fY = (PS - 1) + y + j;
                                        int fZ = (PS - 1) + z + k;
                                        if (pX < EX && pX >= 0 && pY < EY && pY >= 0 && pZ < EZ && pZ >= 0) //check if testing tile is in bounds
                                        {
                                            //loop through matched patterns
                                            foreach (Pattern p in matchedPatterns)
                                            {
                                                if (!cGrid[pX, pY, pZ].IsCollapsed && !cGrid[pX, pY, pZ].Uncollapsable) //check only uncollapsed, collapsable cells
                                                {
                                                    if (CheckForModuleTypeNameInList(p.MPat3d[i, j, k].TypeName, cGrid[pX, pY, pZ].PModules))
                                                    {
                                                        //GHL.AddLine("......... " + p.MPat3d[i, j, k].TypeName + " is valid for cell " + pX + "," + pY + "," + pZ);
                                                        pModuleFlags[fX, fY, fZ][p.MPat3d[i, j, k].TypeName] = true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                //eliminate pmodules
                GHL.AddLine("...2. Eliminate PModules");
                for (int x = -PS + 1; x < PS; x++)
                {
                    for (int y = -PS + 1; y < PS; y++)
                    {
                        for (int z = -PS + 1; z < PS; z++)
                        {
                            int cX = cc.Coord.X + x;
                            int cY = cc.Coord.Y + y;
                            int cZ = cc.Coord.Z + z;
                            if (AllowOutputTilingX)
                            {
                                cX = FindTiledCoord(cX, EX);
                            }
                            if (AllowOutputTilingY)
                            {
                                cY = FindTiledCoord(cY, EY);
                            }
                            if (AllowOutputTilingZ)
                            {
                                cZ = FindTiledCoord(cZ, EZ);
                            }
                            //GHL.AddLine("......Checking pModules to eliminate at " + cX + "," + cY + "," + cZ);
                            if (cX < EX && cX >= 0 && cY < EY && cY >= 0 && cZ < EZ && cZ >= 0)
                            {
                                //GHL.AddLine(".........IsCollapsed: " + cGrid[cX, cY, cZ].IsCollapsed + ", Uncollapsable: " + cGrid[cX, cY, cZ].Uncollapsable);
                            }
                            if (cX < EX && cX >= 0 && cY < EY && cY >= 0 && cZ < EZ && cZ >= 0 && !cGrid[cX, cY, cZ].IsCollapsed && !cGrid[cX, cY, cZ].Uncollapsable)
                            {
                                //eliminate any pModule if the cleared flag is false
                                //GHL.AddLine(". This cell has an entropy of " + cGrid[cX, cY, cZ].Entropy + ". IsCollapsed = " + cGrid[cX, cY, cZ].IsCollapsed);
                                //GHL.AddLine("......Checking pModules to eliminate at " + cX + "," + cY + "," + cZ + ". This cell has an entropy of " + cGrid[cX, cY, cZ].Entropy + ". IsCollapsed = " + cGrid[cX, cY, cZ].IsCollapsed);
                                List<Module> cullMod = new List<Module>();
                                foreach (Module m in cGrid[cX, cY, cZ].PModules)
                                {
                                    //GHL.AddLine("......... checking " + m.TypeName);
                                    if (!pModuleFlags[x + (PS - 1), y + (PS - 1), z + (PS - 1)][m.TypeName])
                                    {
                                        //GHL.AddLine("............ removing " + m.TypeName);
                                        if (cGrid[cX, cY, cZ].PModules.Count > 0 && cGrid[cX, cY, cZ].PModules.Contains(m))
                                        {
                                            //GHL.AddLine("............... flagging for removal");
                                            cullMod.Add(m);
                                        }
                                    }
                                }
                                foreach (Module m in cullMod)
                                {
                                    //GHL.AddLine("......... removing " + m.TypeName);
                                    cGrid[cX, cY, cZ].RemovePModule(m.TypeName);
                                }
                                if (cGrid[cX, cY, cZ].Entropy == 1)
                                {
                                    cGrid[cX, cY, cZ].Collapse(R);
                                    if (!cQ.Contains(cGrid[cX, cY, cZ]))
                                    {
                                        cQ.Enqueue(cGrid[cX, cY, cZ]);
                                        //cQ.Add(cGrid[cX, cY, cZ]);
                                    }
                                    GHL.AddLine(".........Collapsable cell discovered while propagating at " + cX + "," + cY + "," + cZ);
                                }
                                else if (!cGrid[cX, cY, cZ].IsCollapsed && cGrid[cX, cY, cZ].Entropy <= 0)
                                {
                                    GHL.AddLine("!!!!! UNSOLVABLE STATE REACHED AT CELL " + cX + "," + cY + "," + cZ + " !!!!!");
                                    cGrid[cX, cY, cZ].CModule = DefaultModule;
                                    cGrid[cX, cY, cZ].Uncollapsable = true;
                                    UsableCells.Remove(cGrid[cX, cY, cZ]);
                                    //UnSolvable = true;
                                    //goto abortPropagation;
                                }
                            }
                        }
                    }
                }
                GHL.AddLine("...END propagation cycle");
            }
            GHL.AddLine("END propagation queue");
            //abortPropagation:;
        }
        private bool CheckForModuleTypeNameInList(string name, List<Module> mList)
        {
            bool contains = false;
            foreach (Module m in mList)
            {
                if (m.TypeName == name)
                {
                    contains = true;
                    break;
                }
            }
            return contains;
        }
        private int FindTiledCoord(int a, int dim)
        {
            if (a < 0)
            {
                return a + dim;
            }
            else if (a >= dim)
            {
                return a - dim;
            }
            return a;
        }
        private void CullDupAllPatterns() //eliminates duplicate patterns from AllPatterns
        {
            GHL.AddLine("Culling duplicate patterns in AllPatterns");
            List<Module[,,]> mPats = new List<Module[,,]>();
            List<int> indices = new List<int>();
            GHL.AddLine("...Initial pattern list contains " + AllPatterns.Count + " patterns");
            foreach (Pattern p in AllPatterns)
            {
                mPats.Add(p.MPat3d);
            }
            foreach (Module[,,] mPat in mPats) //for every Module array of every Pattern
            {
                for (int i = 0; i < mPats.Count; i++)
                {
                    if (i > mPats.IndexOf(mPat)) //skip the current pattern and all previously checked patterns
                    {
                        int score = 0;
                        for (int x = 0; x < PS; x++)
                        {
                            for (int y = 0; y < PS; y++)
                            {
                                for (int z = 0; z < PS; z++)
                                {
                                    if (mPats[i][x, y, z].TypeName == mPat[x, y, z].TypeName)
                                    {
                                        score++;
                                        if (score >= PS * PS * PS && !indices.Contains(i))
                                        {
                                            indices.Add(i);
                                        }
                                    }
                                    else
                                    {
                                        goto skip;
                                    }
                                }
                            }
                        }
                    }
                skip:;
                }
            }
            GHL.AddLine("Removing " + indices.Count + " items: " + String.Join(", ", indices));
            AllPatterns.RemoveAll(p => indices.Contains(AllPatterns.IndexOf(p)));
            GHL.AddLine("...Final list contains " + AllPatterns.Count + " patterns");
        }
        private List<Pattern> GeneratePatterns(Module[,,] map, int ps) //generates the list of all ps x ps patterns present in the input module map
        {
            GHL.AddLine("Generating Patterns");
            List<Pattern> pList = new List<Pattern>();
            int tix = ExtentsAllowedByTiling(AllowInputTilingX, IX);
            int tiy = ExtentsAllowedByTiling(AllowInputTilingY, IY);
            int tiz = ExtentsAllowedByTiling(AllowInputTilingZ, IZ);
            for (int x = 0; x < tix; x++)
            {
                for (int y = 0; y < tiy; y++)
                {
                    for (int z = 0; z < tiz; z++)
                    {
                        GHL.AddLine("Generating pattern from input grid at " + x + "," + y + "," + z);
                        pList.Add(GetPatternFromModule(x, y, z, ps, map));
                    }
                }
            }
            return pList;
        }
        private int ExtentsAllowedByTiling(bool allow, int axisDim)
        {
            if (allow)
            {
                return axisDim;
            }
            return axisDim - (PS - 1);
        }
        private Pattern GetPatternFromModule(int x, int y, int z, int ps, Module[,,] map)
        {
            GHL.AddLine("......In GetPatternFromModule");
            Module[,,] ma = new Module[ps, ps, ps];
            for (int i = 0; i < ps; i++)
            {
                for (int j = 0; j < ps; j++)
                {
                    for (int k = 0; k < ps; k++)
                    {
                        GHL.AddLine(".........getting pattern from " + x + "," + y + "," + z);
                        int xTiled = x + i; //TODO FIX These can use FindTiledCoord
                        int yTiled = y + j;
                        int zTiled = z + k;
                        if (xTiled >= IX)
                        {
                            xTiled -= IX;
                        }
                        else if (xTiled < 0)
                        {
                            xTiled += IX;
                        }
                        if (yTiled >= IY)
                        {
                            yTiled -= IY;
                        }
                        else if (yTiled < 0)
                        {
                            yTiled += IY;
                        }
                        if (zTiled >= IZ)
                        {
                            zTiled -= IZ;
                        }
                        else if (zTiled < 0)
                        {
                            zTiled += IZ;
                        }
                        ma.SetValue(map[xTiled, yTiled, zTiled], i, j, k);
                    }
                }
            }
            Pattern p = new Pattern(ma);
            GHL.AddLine("......returning pattern p: " + (p != null));
            return p;
        } //checks for patterns near a tile in the input map
        private Module GetModuleByTypeName(string tn) //find a module in the list of all modules by its type name
        {
            foreach (Module m in AllModules)
            {
                if (m.TypeName == tn)
                {
                    return m;
                }
            }
            GHL.AddLine("!!!Module type " + tn + " not found.");
            return null;
        }
    }
    public class Pattern //represents a square piece of the input board, containing NxN modules
    {
        //public Module[,] MPat2d { get; }
        public Module[,,] MPat3d { get; }
        public Pattern(Module[,,] _mPat)
        {
            MPat3d = _mPat;
        }
    }
    public class Coord //an x and y coordinate. Not sure why I made this a class but I'm too lazy to change it right now.
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public Coord(int _x, int _y, int _z)
        {
            X = _x;
            Y = _y;
            Z = _z;
        }
    } 
}