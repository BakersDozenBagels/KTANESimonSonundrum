using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SimonSonundrumScript : MonoBehaviour
{
    [SerializeField]
    private Transform _audioSource;

    [SerializeField]
    private KMAudio _audio;

    [SerializeField]
    private KMBombModule _module;

    [SerializeField]
    private KMSelectable[] _buttons;

    [SerializeField]
    private KMBombInfo _info;

    [SerializeField]
    private TextMesh _stageCount, _textDisplay;

    public static string[] IgnoredModules = null;
    private static int _idc;
    private int _id = ++_idc;

    private int _stage = 0;
    private int _requiredPress = 0;
    private string _requiredSolve;

    private Coroutine _stageRoutine;

    private Func<ValidationInfo, bool> _ruleValidator;

    private bool _prevApplied, _solveOnPress, _isSolved, _solving;

    private void Awake()
    {
        if(IgnoredModules == null)
            IgnoredModules = GetComponent<KMBossModule>().GetIgnoredModules("Simon Sonundrum", new string[] { "Simon Sonundrum" });

        Log("To begin, there is exactly one rule.");
        Log("Whenever and only whenever Simon gives a command that begins with the phrase \"Simon Says:\", follow that command exactly.");
        _ruleValidator = r => r.Rule.Text.StartsWith("Simon Says: ");

        GenerateStage();
    }

    private void Start()
    {
        for(int i = 1; i <= _buttons.Length; i++)
        {
            int j = i;
            _buttons[i - 1].OnInteract += () => { Press(j); return false; };
        }
    }

    private void Press(int j)
    {
        _buttons[j - 1].AddInteractionPunch(0.2f);
        _audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, _buttons[j - 1].transform);
        _audio.PlaySoundAtTransform("Tone-0" + j, _audioSource);

        if(_isSolved)
            return;

        if(_requiredPress == j)
        {
            Log("Good button press.");
            _requiredPress = 0;
            if(_solveOnPress)
            {
                Log("Module solved!");

                _module.HandlePass();

                _isSolved = true;
            }
        }
        else
        {
            Log("You pressed button {0} when you weren't supposed to. Strike!", j);
            _module.HandleStrike();
        }
    }

    private int _ticker = 0;
    private List<string> _prevSolved = new List<string>();

    private void FixedUpdate()
    {
        int solvesRequired = _info.GetSolvableModuleNames().Where(x => !IgnoredModules.Contains(x)).Count();

        _ticker++;
        if(_ticker == 5)
        {
            _ticker = 0;

            List<string> solved = _info.GetSolvedModuleNames().Where(x => !IgnoredModules.Contains(x)).ToList();

            foreach(string s in _prevSolved)
                solved.Remove(s);
            if(solved.Count > 0)
            {
                _prevSolved.AddRange(solved);
                if(_requiredSolve != null)
                {
                    if(solved.Contains(_requiredSolve))
                        Log("Correct solve.");
                    else
                    {
                        Log("Incorrect solve. Strike!");
                        _module.HandleStrike();
                    }
                    _requiredSolve = null;
                }
            }

            int progress = _prevSolved.Count();

            if(!_solving)
            {
                if(progress >= solvesRequired)
                    SolveStage();
                else
                    for(int i = _stage; i < progress; ++i)
                        GenerateStage();
            }

            _stage = progress;
        }
    }

    private void SolveStage()
    {
        StartCoroutine(SolveStageR(_stageRoutine));
    }

    private IEnumerator SolveStageR(Coroutine before)
    {
        _solving = true;

        yield return before;
        yield return null;

        _audio.PlaySoundAtTransform("SimonSays-0" + UnityEngine.Random.Range(1, 5), _audioSource);

        if(_requiredPress != 0)
        {
            Log("You were required to press a button and you didn't. Strike!");
            _module.HandleStrike();
        }

        StartCoroutine(Stage(true));
        ValidationInfo a = new ValidationInfo() { Rule = Rule.DummyA, PreviousApplied = _prevApplied };
        ValidationInfo b = new ValidationInfo() { Rule = Rule.DummyB, PreviousApplied = _prevApplied };
        if(_ruleValidator(a) == false && _ruleValidator(b) == false)
        {
            yield return StartCoroutine(Display("Simon Says: Do nothing."));
            yield return new WaitForSeconds(2f);
            _audio.PlaySoundAtTransform("SimonSays-0" + UnityEngine.Random.Range(1, 5), _audioSource);
            _prevApplied ^= true;
        }

        Rule r;
        do
            r = Rule.RandomFinalRule(_info);
        while(!_ruleValidator(new ValidationInfo() { Rule = r, PreviousApplied = _prevApplied }));

        Log("Simon's last statement: \"{0}\"".Form(r.Text));
        Log("Do apply this rule.");
        Log("This means that you need to press a button.");

        StartCoroutine(Display(r.Text));

        RuleInfo ri = new RuleInfo(this);

        r.Apply(ri);

        _requiredPress = ri.RequiredPress;

        _solveOnPress = true;
    }

    private void GenerateStage()
    {
        _stageRoutine = StartCoroutine(NewStage(_stageRoutine));
    }

    private IEnumerator NewStage(Coroutine before)
    {
        yield return before;
        yield return null;

        _audio.PlaySoundAtTransform("SimonSays-0" + UnityEngine.Random.Range(1, 5), _audioSource);

        if(_requiredPress != 0)
        {
            Log("You were required to press a button and you didn't. Strike!");
            _module.HandleStrike();
        }

        RuleInfo info = new RuleInfo(this);
        Rule rule;
        do
            rule = Rule.RandomRule(_info);
        while(!rule.IsAllowed(info));

        Log("Simon's new statement: \"{0}\"".Form(rule.Text));

        ValidationInfo vi = new ValidationInfo()
        {
            Rule = rule,
            PreviousApplied = _prevApplied
        };

        if(_prevApplied = _ruleValidator(vi))
            rule.Apply(info);

        Log("Do{0} apply this rule.".Form(_prevApplied ? "" : "n't"));

        _requiredPress = info.RequiredPress;
        _requiredSolve = info.RequiredSolve;

        if(_requiredPress != 0)
            Log("This means that you need to press a button.");

        if(info.NewValidator != null)
        {
            _ruleValidator = info.NewValidator;
            Log("This means that the conditions for when to apply rules have changed.");
        }
        if(info.RequiredSolve != null)
            Log("This means that you must solve a specific module next.");

        StartCoroutine(Stage());
        yield return StartCoroutine(Display(rule.Text));
        yield return new WaitForSeconds(2f);
    }

    private IEnumerator Stage(bool final = false)
    {
        string disp = final ? "???" : _stage.ToString("D3");

        while(_stageCount.text != "")
        {
            _stageCount.text = _stageCount.text.Substring(0, _stageCount.text.Length - 1);
            yield return new WaitForSeconds(.1f);
        }

        for(int i = 0; i < disp.Length; i++)
        {
            _stageCount.text += disp[i];
            yield return new WaitForSeconds(.1f);
        }
    }

    private IEnumerator Display(string text)
    {
        string line = "";

        while(_textDisplay.text != "")
        {
            _textDisplay.text = _textDisplay.text.Substring(0, _textDisplay.text.Length - 1);
            yield return new WaitForSeconds(.002f);
        }

        for(int i = 0; i < text.Length; i++)
        {
            line += text[i];
            _textDisplay.text += text[i];

            if(line.Length > 28 && !line.EndsWith(" "))
            {
                _textDisplay.text = _textDisplay.text.Substring(0, _textDisplay.text.LastIndexOf(' '));
                line = line.Substring(line.LastIndexOf(' ') + 1);
                _textDisplay.text += "\n" + line;
            }
            yield return new WaitForSeconds(.002f);
        }
    }

    private void Log(string message, params object[] objs)
    {
        Debug.LogFormat("[Simon Sonundrum #" + _id + "] " + message, objs);
    }

    public class RuleInfo
    {
        public RuleInfo(SimonSonundrumScript script)
        {
            BombInfo = script._info;
            OldValidator = script._ruleValidator;
            PreviousApplied = script._prevApplied;
            Stage = script._stage;
        }

        public KMBombInfo BombInfo;

        public Func<ValidationInfo, bool> OldValidator;

        public bool PreviousApplied;

        public int Stage;



        public int RequiredPress;

        public Func<ValidationInfo, bool> NewValidator;

        internal string RequiredSolve;
    }

    public class ValidationInfo
    {
        public Rule Rule;

        public bool PreviousApplied;
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Use ""!{0} tl"" to press that button. Valid buttons are tl, tr, bl, and br. ";
#pragma warning restore 414

    private KMSelectable[] ProcessTwitchCommand(string command)
    {
        switch(command.Trim().ToLowerInvariant())
        {
            case "tl":
                return new KMSelectable[] { _buttons[0] };
            case "tr":
                return new KMSelectable[] { _buttons[1] };
            case "bl":
                return new KMSelectable[] { _buttons[2] };
            case "br":
                return new KMSelectable[] { _buttons[3] };
        }

        return null;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        Log("Module force solved.");
        StartCoroutine(AutoSolve());
        while(!_isSolved)
            yield return true;
    }

    private IEnumerator AutoSolve()
    {
        while(!_isSolved)
        {
            yield return new WaitWhile(() => _requiredPress == 0);
            _buttons[_requiredPress - 1].OnInteract();
        }
    }
}