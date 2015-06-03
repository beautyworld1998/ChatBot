﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Xml;
using System.Collections;

namespace QXS.ChatBot
{
    public class ConditionBotRule : BotRule
    {
        public enum Operator {
            Equal,
            EqualIgnoreCase,
            Unequal,
            UnequalIgnoreCase,
        }

        protected IEnumerable<Tuple<string, Operator, string>> _Conditions;
        protected SortedList<int, List<BotRule>> _BotRules = new SortedList<int, List<BotRule>>(new DescComparer<int>());

        public ConditionBotRule(string Name, int Weight, IEnumerable<Tuple<string, Operator, string>> Conditions, IEnumerable<BotRule> Rules)
            : base(Name, Weight)
        {
            this._MessagePattern = new Regex("^.*$");
            this._Conditions = Conditions;
            Dictionary<string, bool> ruleNames = new Dictionary<string, bool>();
            foreach (BotRule rule in Rules)
            {
                if (rule.Process == null)
                {
                    throw new ArgumentException("Process is null.", "Rules");
                }
                if (rule.MessagePattern == null)
                {
                    throw new ArgumentException("MessagePattern is null.", "Rules");
                }
                if (ruleNames.ContainsKey(rule.Name))
                {
                    throw new ArgumentException("Names are not unique. Duplicate key found for rule name \"" + rule.Name + "\".", "Rules");
                }
                ruleNames[rule.Name] = true;
                if (!this._BotRules.ContainsKey(rule.Weight))
                {
                    this._BotRules[rule.Weight] = new List<BotRule>();
                }
                this._BotRules[rule.Weight].Add(rule);
            }

            this._Process = this.ProcessSubrules;
        }

        public string ProcessSubrules(Match match, ChatSessionInterface session)
        {
            foreach (Tuple<string, Operator, string> condition in this._Conditions)
            {
                if (!session.SessionStorage.Values.ContainsKey(condition.Item1))
                {
                    return null;
                }
                switch(condition.Item2)
                {
                    case Operator.Equal:
                        if (session.SessionStorage.Values[condition.Item1] != condition.Item3)
                        {
                            return null;
                        }
                        break;
                    case Operator.Unequal:
                        if (session.SessionStorage.Values[condition.Item1] == condition.Item3)
                        {
                            return null;
                        }
                        break;
                    case Operator.EqualIgnoreCase:
                        if (session.SessionStorage.Values[condition.Item1].ToLower() != condition.Item3.ToLower())
                        {
                            return null;
                        }
                        break;
                    case Operator.UnequalIgnoreCase:
                        if (session.SessionStorage.Values[condition.Item1].ToLower() == condition.Item3.ToLower())
                        {
                            return null;
                        }
                        break;
                }
            }

            foreach (List<BotRule> rules in this._BotRules.Values)
            {
                foreach (BotRule rule in rules)
                {
                    Match submatch = rule.MessagePattern.Match(match.Value);
                    if (submatch.Success)
                    {

                        string msg = rule.Process(submatch, session);
                        if (msg != null)
                        {
                            return msg;
                        }
                    }
                }
            }
            // no hit found
            return null;
        }

        new public static BotRule CreateRuleFromXml(ChatBotRuleGenerator generator, XmlNode node)
        {
            // get unique setters
            List<Tuple<string, Operator, string>> conditions = new List<Tuple<string, Operator, string>>();
            foreach (XmlNode subnode in node.SelectNodes("Conditions/Condition").Cast<XmlNode>().Where(n => n.Attributes["Key"] != null && n.Attributes["Operator"]!=null))
            {
                switch (subnode.Attributes["Operator"].Value.Trim().ToLower())
                {
                    case "equal":
                    case "eq":
                        conditions.Add(new Tuple<string,Operator,string>(subnode.Attributes["Key"].Value, Operator.Equal, subnode.InnerText));
                        break;
                    case "equalignorecase":
                    case "ieq":
                        conditions.Add(new Tuple<string, Operator, string>(subnode.Attributes["Key"].Value, Operator.EqualIgnoreCase, subnode.InnerText));
                        break;
                    case "unequal":
                    case "ne":
                        conditions.Add(new Tuple<string, Operator, string>(subnode.Attributes["Key"].Value, Operator.Unequal, subnode.InnerText));
                        break;
                    case "unequalignorecase":
                    case "ine":
                        conditions.Add(new Tuple<string, Operator, string>(subnode.Attributes["Key"].Value, Operator.UnequalIgnoreCase, subnode.InnerText));
                        break;

                }
                
            }

            return new ConditionBotRule(
                generator.GetRuleName(node),
                generator.GetRuleWeight(node),
                conditions,
                generator.Parse(node.OwnerDocument, node)
            );
        }
    }

    public class ReplacementBotRule : BotRule
    {
        protected Random rnd = new Random();
        protected string[] _Replacements;
        protected Regex _Regex = new Regex("\\$(r|s)\\$([a-z0-9]+)\\$", RegexOptions.IgnoreCase);
        protected Dictionary<string, string> _setters = new Dictionary<string, string>();

        public ReplacementBotRule(string Name, int Weight, Regex MessagePattern, string Replacement, Dictionary<string, string> setters)
            : this(Name, Weight, MessagePattern, Replacement)
        {
            this._setters = setters;
        }
        public ReplacementBotRule(string Name, int Weight, Regex MessagePattern, string[] Replacements, Dictionary<string, string> setters)
            : this(Name, Weight, MessagePattern, Replacements)
        {
            this._setters = setters;
        }

        public ReplacementBotRule(string Name, int Weight, Regex MessagePattern, string Replacement)
            : base(Name, Weight, MessagePattern)
        {
            this._Replacements = new string[] { Replacement };
            this._Process = this.ReplaceMessage;
        }

        public ReplacementBotRule(string Name, int Weight, Regex MessagePattern, string[] Replacements)
            : base(Name, Weight, MessagePattern)
        {
            this._Replacements = Replacements;
            this._Process = this.ReplaceMessage;
        }

        public string ReplaceMessage(Match match, ChatSessionInterface session)
        {
            // set the setters
            foreach (string key in this._setters.Keys)
            {
                session.SessionStorage.Values[key] = this._Regex.Replace(
                    this._setters[key],
                    (Match m) =>
                    {
                        switch (m.Groups[1].Value.ToLower())
                        {
                            case "s":
                                if (session.SessionStorage.Values.ContainsKey(m.Groups[2].Value))
                                {
                                    return session.SessionStorage.Values[m.Groups[2].Value];
                                }
                                break;
                            case "r":
                                return match.Groups[m.Groups[2].Value].Value;
                        }
                        return "";
                    }
                );
                
            }

            // send a anwer

            if (this._Replacements.Length == 0)
            {
                return "";
            }
            string msg;
            if (this._Replacements.Length > 1)
            {
                msg = this._Replacements[rnd.Next(this._Replacements.Length)];
            }
            else
            {
                msg = this._Replacements[0];
            }

            return this._Regex.Replace(
                msg,
                (Match m) =>
                {
                    switch(m.Groups[1].Value.ToLower())
                    {
                        case "s":
                            if (session.SessionStorage.Values.ContainsKey(m.Groups[2].Value))
                            {
                                return session.SessionStorage.Values[m.Groups[2].Value];
                            }
                            break;
                        case "r":
                            return match.Groups[m.Groups[2].Value].Value;
                    }
                    return "";
                }
            );
        }

        new public static BotRule CreateRuleFromXml(ChatBotRuleGenerator generator, XmlNode node)
        {
            // get unique setters
            Dictionary<string, string> setters = new Dictionary<string, string>();
            foreach (XmlNode subnode in node.SelectNodes("Setters/Set").Cast<XmlNode>().Where(n => n.Attributes["Key"] != null))
            {
                setters[subnode.Attributes["Key"].Value] = subnode.InnerText;
            }

            return new ReplacementBotRule(
                generator.GetRuleName(node),
                generator.GetRuleWeight(node),
                new Regex(generator.GetRulePattern(node)),
                node.SelectNodes("Messages/Message").Cast<XmlNode>().Select(n => n.InnerText).ToArray(),
                setters
            );
        }
    }

    public class RandomAnswersBotRule : BotRule
    {
        protected Random rnd = new Random();

        protected string[] _messages; 

        public RandomAnswersBotRule(string Name, int Weight, Regex MessagePattern, string[] Messages)
            : base(Name, Weight, MessagePattern)
        {
            this._messages = Messages;
            this._Process = this.SendRandomMessage;
        }

        public string SendRandomMessage(Match match, ChatSessionInterface session)
        {
            return this._messages[rnd.Next(this._messages.Length)];
        }

        new public static BotRule CreateRuleFromXml(ChatBotRuleGenerator generator, XmlNode node)
        {
            return new RandomAnswersBotRule(
                generator.GetRuleName(node),
                generator.GetRuleWeight(node),
                new Regex(generator.GetRulePattern(node)),
                node.SelectNodes("Messages/Message").Cast<XmlNode>().Select(n => n.InnerText).ToArray()
            );
        }
    }

    public class BotRule
    {
        protected BotRule(string Name, int Weight)
        {
            if (Name == null)
            {
                throw new ArgumentException("Name is null.", "Name");
            }

            this._Name = Name;
            this._Weight = Weight;
        }

        protected BotRule(string Name, int Weight, Regex MessagePattern)
            : this(Name, Weight)
        {
            if (MessagePattern == null)
            {
                throw new ArgumentException("MessagePattern is null.", "MessagePattern");
            }
            this._MessagePattern = MessagePattern;
        }

        public BotRule(string Name, int Weight, Regex MessagePattern, Func<Match, ChatSessionInterface, string> Process)
            : this(Name, Weight, MessagePattern)
        {
            if (Process == null)
            {
                throw new ArgumentException("Process is null.", "Process");
            }
            this._Process = Process;
        }

        protected string _Name;
        public string Name { get { return _Name; } }

        protected int _Weight;
        public int Weight { get { return _Weight; } }

        protected Regex _MessagePattern;
        public Regex MessagePattern { get { return _MessagePattern; } }

        protected Func<Match, ChatSessionInterface, string> _Process;
        public Func<Match, ChatSessionInterface, string> Process { get { return _Process; } }

        public static BotRule CreateRuleFromXml(ChatBotRuleGenerator generator, XmlNode node)
        {
            return new BotRule(
                generator.GetRuleName(node), 
                generator.GetRuleWeight(node), 
                new Regex(generator.GetRulePattern(node)), 
                delegate(Match match, ChatSessionInterface session) { 
                    return "I have to think about that";  
                } 
           );
        }
    }

}
