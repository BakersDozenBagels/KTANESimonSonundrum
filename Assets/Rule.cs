using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

public class Rule
{
    private string Prefix { get; set; }

    private string Body { get; set; }

    public string Text { get { return Prefix + Body; } }

    public Action<SimonSonundrumScript.RuleInfo> Apply { get; private set; }

    public Func<SimonSonundrumScript.RuleInfo, bool> IsAllowed { get; private set; }

    public Rule(bool noFix = false)
    {
        Prefix = Random.Range(0, 2) == 1 && !noFix ? "Simon Says: " : "";
    }

    public static Rule RandomRule(KMBombInfo info)
    {
        int selection = Random.Range(0, _ruleWeightSum);

        for(int i = 0; i < _ruleGenerators.Count; ++i)
        {
            selection -= _ruleGenerators[i].Weight;
            if(selection < 0)
                return _ruleGenerators[i].RandomRule(info);
        }

        throw new Exception();
    }

    public static Rule RandomFinalRule(KMBombInfo info)
    {
        return _finalRuleGenerator.RandomRule(info);
    }

    private static List<RuleGenerator> _ruleGenerators;
    private static RuleGenerator _finalRuleGenerator;
    private static int _ruleWeightSum;

    static Rule()
    {
        _ruleGenerators = new List<RuleGenerator>
        {
            new ButtonRuleGenerator(),
            new TextValidatorRuleGenerator(),
            new InvertValidatorRuleGenerator(),
            new SolveNextRuleGenerator(),
            new ParityValidatorRuleGenerator(),
            new JuxtaRuleGenerator(),
            //new DuckRuleGenerator(),
        };

        _finalRuleGenerator = new ButtonRuleGenerator();

        _ruleWeightSum = _ruleGenerators.Select(x => x.Weight).Sum();
    }

    private abstract class RuleGenerator
    {
        public abstract Rule RandomRule(KMBombInfo info);
        public abstract int Weight { get; }
    }

    private sealed class ButtonRuleGenerator : RuleGenerator
    {
        public override Rule RandomRule(KMBombInfo _)
        {
            int button = Random.Range(0, 4);
            string buttonName = "ERROR";
            switch(button)
            {
                case 0:
                    buttonName = "top-left";
                    break;
                case 1:
                    buttonName = "top-right";
                    break;
                case 2:
                    buttonName = "bottom-left";
                    break;
                case 3:
                    buttonName = "bottom-right";
                    break;
            }
            return new Rule
            {
                Body = "Press the {0} button.".Form(buttonName),
                Apply = i => i.RequiredPress = button + 1,
                IsAllowed = i => true
            };

        }

        public override int Weight { get { return 7; } }
    }

    private sealed class TextValidatorRuleGenerator : RuleGenerator
    {
        public override Rule RandomRule(KMBombInfo _)
        {
            int mode = Random.Range(0, 2);
            string tex = "ERROR";
            Func<SimonSonundrumScript.ValidationInfo, bool> validator = null;
            switch(mode)
            {
                case 0:
                    int parity = Random.Range(0, 2);
                    tex = "they have an {0} amount of vowels in them. (Y is not a vowel.)".Form(parity == 1 ? "odd" : "even");
                    char[] vowels = "aeiouAEIOU".ToCharArray();
                    validator = r => r.Rule.Text.Count(c => vowels.Contains(c)) % 2 == parity;
                    break;
                case 1:
                    parity = Random.Range(0, 2);
                    tex = "they have an {0} amount of the letter I in them.".Form(parity == 1 ? "odd" : "even");
                    char[] e = "iI".ToCharArray();
                    validator = r => r.Rule.Text.Count(c => e.Contains(c)) % 2 == parity;
                    break;
            }
            return new Rule
            {
                Body = "Follow my commands when and only when {0}".Form(tex),
                Apply = i => i.NewValidator = validator,
                IsAllowed = i => true
            };
        }

        public override int Weight { get { return 2; } }
    }

    private sealed class InvertValidatorRuleGenerator : RuleGenerator
    {
        public override Rule RandomRule(KMBombInfo _)
        {
            return new Rule
            {
                Body = "Follow my commands when and only when you wouldn't have immediately before this command.",
                Apply = i => i.NewValidator = vi => !i.OldValidator(vi),
                IsAllowed = i => true
            };
        }

        public override int Weight { get { return 1; } }
    }

    private sealed class SolveNextRuleGenerator : RuleGenerator
    {
        public override Rule RandomRule(KMBombInfo info)
        {
            bool valid = true;
            string modName = "ERROR";
            IEnumerable<string> allMods = info.GetSolvableModuleNames().Except(info.GetSolvedModuleNames()).Where(n => !SimonSonundrumScript.IgnoredModules.Contains(n));
            if(allMods.Count() == 0)
                valid = false;
            IEnumerable<string> mods = new string[] { "Organization", "Mytery Module", "Encrypted Hangman", "Turn The Keys", "Custom Keys", "42", "501", "The Heart", "Simon" }.Concat(SimonSonundrumScript.IgnoredModules).Except(new string[] { "Simon Sonundrum" });
            if(info.GetSolvableModuleNames().Any(n => mods.Contains(n)) || info.GetSolvableModuleNames().Count(s => s == "Simon Sonundrum") > 1)
                valid = false;
            if(valid)
                modName = allMods.PickRandom();
            return new Rule
            {
                Body = "Solve {0} next.".Form(modName),
                Apply = i => i.RequiredSolve = modName,
                IsAllowed = i => valid
            };
        }

        public override int Weight { get { return 1; } }
    }

    private sealed class ParityValidatorRuleGenerator : RuleGenerator
    {
        public override Rule RandomRule(KMBombInfo info)
        {
            return new Rule
            {
                Body = "Follow my commands when and only when you didn't follow the previous command.",
                Apply = i => i.NewValidator = vi => !vi.PreviousApplied,
                IsAllowed = i => true
            };
        }

        public override int Weight { get { return 1; } }
    }

    private sealed class JuxtaRuleGenerator : RuleGenerator
    {
        public override Rule RandomRule(KMBombInfo info)
        {
            Rule a, b;
            do
                a = Rule.RandomRule(info);
            while(a.Body.StartsWith("If"));
            do
                b = Rule.RandomRule(info);
            while(b.Body.StartsWith("If") || b.Body == a.Body);

            return new Rule
            {
                Body = "If you followed my previous command, {0} Otherwise, {1}".Form(a.Body, b.Body),
                Apply = i => (i.PreviousApplied ? a : b).Apply(i),
                IsAllowed = i => i.Stage != 0 && a.IsAllowed(i) && b.IsAllowed(i)
            };
        }

        public override int Weight { get { return 1; } }
    }

    //private sealed class DuckRuleGenerator : RuleGenerator
    //{
    //    public override Rule RandomRule(KMBombInfo info)
    //    {
    //        bool valid = true;
    //        string cond = "ERROR", act = "ERROR";
    //        UnityEngine.Component[] konundrums = info.transform.parent.GetComponentsInChildren(ReflectionHelper.FindType("duckKonundrumScript"));
    //        if(konundrums.Length == 0)
    //            valid = false;

    //        return new Rule
    //        {
    //            Body = "On the Duck Konundrum where {0}, {1}.".Form(cond, act),
    //            Apply = i => { },
    //            IsAllowed = i => valid
    //        };
    //    }

    //    public override int Weight { get { return 1; } }
    //}

    public static Rule DummyA
    {
        get
        {
            return new Rule(true)
            {
                Body = "I"
            };
        }
    }

    public static Rule DummyB
    {
        get
        {
            return new Rule(true)
            {
                Body = ""
            };
        }
    }
}