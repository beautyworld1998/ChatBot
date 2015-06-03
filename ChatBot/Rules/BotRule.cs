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
