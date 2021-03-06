﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using SimpleScanner = SCS.System.SimpleCommandScanner;
using StringScanner = SCS.System.StringCommandScanner;

namespace SCS.System
{
    public class Command
    {
        /// <summary>Condition for the find method.</summary>
        public enum FindCondition
        {
            /// <summary>Find method returns <see langword="true"/> if all filters are true.</summary>
            And,
            /// <summary>Find method returns <see langword="true"/> if one of filters is true.</summary>
            Or
        }

        private static string _standardPrefix = "/";
        /// <summary>It will be sets for a command if it's prefix is <see langword="null"/> in the command attribute. The default is "/".</summary>
        public static string StandardPrefix 
        {
            get
            {
                return _standardPrefix;
            }
            set
            {
                _standardPrefix = value ?? String.Empty;
            }
        }

        private static string _standardDescription = "No description.";
        /// <summary>It will be sets for a command if it's description is <see langword="null"/> in the command attribute. The default is "No description".</summary>
        public static string StandardDescription
        {
            get
            {
                return _standardDescription;
            }
            set
            {
                _standardDescription = value ?? String.Empty;
            }
        }

        /// <summary>The list of all used prefixes in commands from registered classes.</summary>
        public static List<string> Prefixes { get; private set; } = new List<string>();

        /// <summary>The list of all commands from registered classes.</summary>
        public static List<Command> Commands { get; private set; } = new List<Command>();

        private string _prefix;
        public string Prefix
        {
            get
            {
                return _prefix;
            }
            private set
            {
                _prefix = value;
                if (!Prefixes.Contains(value))
                {
                    Prefixes.Add(value);
                }
            }
        }

        private string _name;
        public string Name
        {
            get
            {
                return _name;
            }
            private set
            {
                _name = value.Replace(" ", "");
            }
        }

        public string Description { get; private set; } 
        public List<string> Tags { get; private set; } 

        public readonly MethodInfo Method;
        public readonly MemberInfo Class;

        public readonly ParameterInfo[] Parameters;
        public readonly bool ContainsParameters;

        public Command(MethodInfo method, CommandAttribute attribute)
        {
            Method = method;
            Class = method.DeclaringType;

            Parameters = method.GetParameters();
            ContainsParameters = Parameters.Length > 0;

            Prefix = attribute.Prefix;
            Name = attribute.Name;
            Description = attribute.Description;
            Tags = attribute.Tags.ToList();
        }

        /// <summary>Prepares commands from the class for use.</summary>
        /// <typeparam name="TClassWithCommands">The class with commands.</typeparam>
        public static void RegisterCommands<TClassWithCommands>()
        {
            Type type = typeof(TClassWithCommands);
            RegisterCommands(type);
        }

        /// <summary>Prepares commands from the class for use.</summary>
        /// <param name="classesWithCommands">The classes with commands.</param>
        public static void RegisterCommands(params Type[] classesWithCommands)
        {
            foreach (Type type in classesWithCommands)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

                foreach (MethodInfo method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(CommandAttribute));

                    foreach (var attribute in attributes)
                    {
                        Commands.Add(new Command(method, (CommandAttribute)attribute));
                    }
                }
            }
        }

        /// <summary>
        /// Finds and returns a list of commands matching the all specified filters, i.e condition is FindCondition.And.
        /// <para>Available filters: SimpleCommandScanner (Equals/NotEquals), StringCommandScanner (Contains/NotContains), ListCommandScanner (Contains/NotContains)</para>
        /// </summary>
        public static List<Command> Find(params CommandScanner[] filters)
        {
            return Find(FindCondition.And, filters);
        }

        /// <summary>
        /// Finds and returns a list of commands matching the specified filters by the specified condition.
        /// <para>Available filters: SimpleCommandScanner (Equals/NotEquals), StringCommandScanner (Contains/NotContains), ListCommandScanner (Contains/NotContains)</para>
        /// <param name="condition">If condition is FindCondition.And, returns is <see langword="true"/> if all filters are true.
        /// If condition is FindCondition.Or, returns is <see langword="true"/> if one of filters is true</param>
        /// </summary>
        public static List<Command> Find(FindCondition condition, params CommandScanner[] filters)
        {
            List<Command> foundCommands = new List<Command>();
            foreach (Command command in Commands)
            {
                foreach (CommandScanner commandScanner in filters)
                {
                    if (condition == FindCondition.And)
                    {
                        if (commandScanner.Scan(command) == false)
                        {
                            break;
                        }
                        else if (filters.Last() == commandScanner)
                        {
                            foundCommands.Add(command);
                        }
                    }
                    else
                    {
                        if (commandScanner.Scan(command) == true && !foundCommands.Contains(command))
                        {
                            foundCommands.Add(command);
                        }
                    }
                }
            }
            return foundCommands;
        }

        /// <summary>
        /// Parses the message to a command and executes it.
        /// If the message by arguments matches several commands, the one that was added earlier is called.
        /// </summary>
        public static void Execute(string message)
        {
            List<string> words = new List<string>();
            List<string> matchingPrefixes = new List<string>();

            string commandPrefix;
            string commandName;
            List<object> arguments = new List<object>();

            List<Command> matchingCommands;

            #region Finding a prefix
            foreach (string prefix in Prefixes)
            {
                if (message.IndexOf(prefix) == 0)
                {
                    matchingPrefixes.Add(prefix);
                }
            }

            if (matchingPrefixes.Count == 0)
            {
                commandPrefix = String.Empty;
            }
            else
            {
                commandPrefix = matchingPrefixes.OrderByDescending(s => s.Length).First();
            }
            #endregion

            #region Finding matching commands and arguments
            message = message.Substring(commandPrefix.Length);

            words.AddRange(Regex.Split(message, " (?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)").Select(s => s.Replace("\"", "")));
            words.RemoveAll(i => i == String.Empty);

            if (words.Count == 0)
            {
                AdvancedConsole.Warn(AdvancedConsole.WarningType.WrongCommand);
                return;
            }

            commandName = words[0];
            words.Remove(commandName);
            arguments.AddRange(words.Select(s => s));

            StringScanner prefixScanner = new StringScanner(commandPrefix, StringScanner.TargetOfScanner.Prefix);
            StringScanner nameScanner = new StringScanner(commandName, StringScanner.TargetOfScanner.Name);

            if (arguments.Count > 0)
            {
                SimpleScanner containsParametersScanner = new SimpleScanner(true, SimpleScanner.TargetOfScanner.ContainsParameters);
                matchingCommands = Find(prefixScanner, nameScanner, containsParametersScanner);
                matchingCommands.Reverse();
            }
            else
            {
                matchingCommands = Find(prefixScanner, nameScanner);
            }

            if (matchingCommands.Count == 0)
            {
                AdvancedConsole.Warn(AdvancedConsole.WarningType.WrongCommand);
                return;
            }
            #endregion

            #region Executing the most matching command
            foreach (Command command in matchingCommands)
            {
                if (!command.ContainsParameters)
                {
                    command.Execute();
                    return;
                }
                else
                {
                    ParameterInfo[] parametersInfo = command.Method.GetParameters();

                    // TODO: Parse for commands with the last parameter is an array

                    try
                    {
                        for (int i = 0; i < parametersInfo.Length; i++)
                        {
                            Type type = parametersInfo[i].ParameterType;
                            if (i < arguments.Count)
                            {
                                arguments[i] = Convert.ChangeType(arguments[i], type);
                            }
                            else
                            {
                                if (parametersInfo[i].HasDefaultValue)
                                {
                                    arguments.Add(parametersInfo[i].DefaultValue);
                                }
                                else
                                {
                                    throw new ArgumentException();
                                }
                            }
                        }
                    }
                    catch
                    {
                        if (command == matchingCommands.Last())
                        {
                            AdvancedConsole.Warn(AdvancedConsole.WarningType.WrongArguments);
                        }
                        continue;
                    }

                    command.Execute(arguments.ToArray());
                    return;
                }
            }
            #endregion
        }

        /// <summary>Executes the command.</summary>
        public void Execute(params object[] parameters)
        {
            try
            {
                Method.Invoke(null, parameters);
            }
            catch
            {
                AdvancedConsole.Warn(AdvancedConsole.WarningType.WrongArguments);
            }
        }
    }
}
