// Copyright (c) 2002-2015 SIL International
// This software is licensed under the LGPL, version 2.1 or later
// (http://www.gnu.org/licenses/lgpl-2.1.html)
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Threading;
using antlr;

namespace SIL.FieldWorks.Tools
{
	/// ----------------------------------------------------------------------------------------
	/// <summary>
	/// Imports the interfaces of an IDL file.
	/// </summary>
	/// ----------------------------------------------------------------------------------------
	public class IDLImporter
	{
		private static IdhCommentProcessor s_idhProcessor;
		internal static Dictionary<string, IdhCommentProcessor.CommentInfo> s_MoreComments =
			new Dictionary<string,IdhCommentProcessor.CommentInfo>();
		private EventWaitHandle m_waitHandle;

		private CodeCommentStatementCollection AddFileBanner(string sInFile, string sOutFile)
		{
			CodeCommentStatementCollection coll = new CodeCommentStatementCollection();

			coll.Add(new CodeCommentStatement("--------------------------------------------------------------------------------------------"));
			coll.Add(new CodeCommentStatement(string.Format(
				"Copyright (c) {0}, SIL International. All rights reserved.", DateTime.Now.Year)));
			coll.Add(new CodeCommentStatement(""));
			coll.Add(new CodeCommentStatement("File: " + Path.GetFileName(sOutFile)));
			coll.Add(new CodeCommentStatement("Responsibility: Generated by IDLImporter"));
			coll.Add(new CodeCommentStatement("Last reviewed: "));
			coll.Add(new CodeCommentStatement(""));
			coll.Add(new CodeCommentStatement("<remarks>"));
			coll.Add(new CodeCommentStatement("Generated by IDLImporter from file " + Path.GetFileName(sInFile)));
			coll.Add(new CodeCommentStatement(""));
			coll.Add(new CodeCommentStatement("You should use these interfaces when you access the COM objects defined in the mentioned"));
			coll.Add(new CodeCommentStatement("IDL/IDH file."));
			coll.Add(new CodeCommentStatement("</remarks>"));
			coll.Add(new CodeCommentStatement("--------------------------------------------------------------------------------------------"));
			return coll;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Add comments to each class and method
		/// </summary>
		/// <param name="types">Collection of types</param>
		/// ------------------------------------------------------------------------------------
		private void AddComments(CodeTypeDeclarationCollection types)
		{
			foreach (CodeTypeDeclaration type in types)
			{
				// we probably inherited the comments (from a base class in an external file),
				// so we don't want to add the same comments again!
				if (type.Comments.Count > 0)
					continue;

				string comment = type.Name;
				IdhCommentProcessor.CommentInfo ifaceComment = null;
				if (!s_idhProcessor.Comments.TryGetValue(type.Name, out ifaceComment) && type.Name != string.Empty)
					s_idhProcessor.Comments.TryGetValue(type.Name.Substring(1), out ifaceComment);

				// Also get comments for base class - if we derive from a class
				// we might need to get some comments from there if we don't have our own.
				List<IdhCommentProcessor.CommentInfo> baseComments =
					new List<IdhCommentProcessor.CommentInfo>();

				if (type.BaseTypes.Count > 0)
				{
					for (int i = 0; i < type.BaseTypes.Count; i++)
					{
						IdhCommentProcessor.CommentInfo baseComment = null;
						if (!s_idhProcessor.Comments.TryGetValue(type.BaseTypes[i].BaseType,
							out baseComment))
						{
							s_idhProcessor.Comments.TryGetValue(type.BaseTypes[i].BaseType.Substring(1),
								out baseComment);
						}
						if (baseComment != null)
							baseComments.Add(baseComment);
					}
				}

				if (ifaceComment != null)
					comment = ifaceComment.Comment;

				type.Comments.Add(new CodeCommentStatement(
					string.Format(comment.Length > 80 ? "<summary>{1}{0}{1}</summary>" :
					"<summary>{0} </summary>", comment, Environment.NewLine), true));

				foreach (CodeTypeMember member in type.Members)
				{
					if ((!type.IsInterface && !type.IsEnum &&
						(member.Attributes & MemberAttributes.Private) == MemberAttributes.Private)
						|| member.Comments.Count > 0 || member.Name == string.Empty)
					{
						continue;
					}

					IdhCommentProcessor.CommentInfo methodComment = null;
					if (ifaceComment != null)
						ifaceComment.Children.TryGetValue(member.Name, out methodComment);

					for (int i = 0; i < baseComments.Count && methodComment == null; i++)
						baseComments[i].Children.TryGetValue(member.Name, out methodComment);

					if (member is CodeMemberMethod)
					{
						if (methodComment == null)
						{
							// Maybe it's a property with a parameter? Try and see if the IDH
							// file has a comment for a method without the "get_" or "set_"
							if (member.Name.StartsWith("get_") || member.Name.StartsWith("set_"))
							{
								string name = member.Name.Substring(4);
								if (ifaceComment != null)
									ifaceComment.Children.TryGetValue(name, out methodComment);

								for (int i = 0; i < baseComments.Count && methodComment == null; i++)
									baseComments[i].Children.TryGetValue(name, out methodComment);
							}
						}

						comment = "Member " + member.Name;
						if (methodComment != null)
							comment = methodComment.Comment;

						member.Comments.Add(new CodeCommentStatement(
							string.Format(comment.Length > 80 ? "<summary>{1}{0}{1}</summary>" :
							"<summary>{0} </summary>", comment, Environment.NewLine), true));

						CodeMemberMethod method = member as CodeMemberMethod;
						foreach (CodeParameterDeclarationExpression param in method.Parameters)
						{
							IdhCommentProcessor.CommentInfo paramComment = null;
							if (methodComment != null)
								methodComment.Children.TryGetValue(param.Name, out paramComment);

							comment = string.Empty;
							if (paramComment != null)
								comment = paramComment.Comment;
							member.Comments.Add(new CodeCommentStatement(
								string.Format("<param name='{0}'>{1} </param>", param.Name, comment),
								true));
						}

						if (method.ReturnType.BaseType != "System.Void")
						{
							comment = "A " + method.ReturnType.BaseType;
							if (methodComment != null && methodComment.Attributes.ContainsKey("retval"))
							{
								string retparamName = methodComment.Attributes["retval"];
								if (methodComment.Children.ContainsKey(retparamName))
									comment = methodComment.Children[retparamName].Comment;
							}
							member.Comments.Add(new CodeCommentStatement(
								string.Format("<returns>{0}</returns>", comment), true));
						}
					}
					else if (member is CodeMemberProperty)
					{
						CodeMemberProperty property = member as CodeMemberProperty;

						string getset = string.Empty;
						if (methodComment == null)
						{
							// No comment from IDH file - generate a pseudo one
							if (property.HasGet)
								getset += "Gets";
							if (property.HasSet)
							{
								if (getset.Length > 0)
									getset += "/";
								getset += "Sets";
							}
							getset = string.Format("{0} a {1}", getset, member.Name);
						}
						else
						{
							// Use comment provided in IDH file
							getset = methodComment.Comment;
						}

						member.Comments.Add(new CodeCommentStatement(
							string.Format(getset.Length > 80 ? "<summary>{1}{0}{1}</summary>" :
							"<summary>{0} </summary>", getset, Environment.NewLine), true));
						member.Comments.Add(new CodeCommentStatement(
							string.Format("<returns>A {0} </returns>",
							property.Type.BaseType), true));
					}
					else if (member is CodeMemberField)
					{
						if (methodComment == null)
						{	// No comment from IDH file - generate a pseudo one
							comment = string.Empty;
						}
						else
						{	// Use comment provided in IDH file
							comment = methodComment.Comment;
						}

						member.Comments.Add(new CodeCommentStatement(
							string.Format(comment.Length > 80 ? "<summary>{1}{0}{1}</summary>" :
							"<summary>{0} </summary>", comment, Environment.NewLine), true));
					}
					else
					{
						comment = "Member " + member.Name;
						if (methodComment != null)
							comment = methodComment.Comment;

						member.Comments.Add(new CodeCommentStatement(
							string.Format(comment.Length > 80 ? "<summary>{1}{0}{1}</summary>" :
							"<summary>{0} </summary>", comment, Environment.NewLine), true));

						member.Comments.Add(new CodeCommentStatement(
							string.Format("Not expecting a member of type {0}",
							member.GetType())));
					}

					if (methodComment != null && methodComment.Attributes.ContainsKey("exception"))
					{
						string[] exceptions = methodComment.Attributes["exception"].Split(',');
						foreach (string exception in exceptions)
						{
							comment = methodComment.Attributes[exception];
							member.Comments.Add(new CodeCommentStatement(
								string.Format(@"<exception cref=""{0}"">{1}</exception>",
								exception, comment), true));
						}
					}
				}
			}
		}



		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Do the actual import
		/// </summary>
		/// <param name="usingNamespaces">Additional imported namespaces</param>
		/// <param name="sFileName">Filename of the IDL file</param>
		/// <param name="sXmlFile">Name of the XML config file</param>
		/// <param name="sOutFile">Output</param>
		/// <param name="sNamespace">Namespace</param>
		/// <param name="idhFiles">Names of IDH file used to retrieve comments.</param>
		/// <param name="referencedFiles">Names of files used to resolve references to
		/// external types.</param>
		/// <param name="fCreateComments"><c>true</c> to create XML comments</param>
		/// <returns><c>true</c> if import successful, otherwise <c>false</c>.</returns>
		/// ------------------------------------------------------------------------------------
		public bool Import(List<string> usingNamespaces, string sFileName, string sXmlFile,
			string sOutFile, string sNamespace, StringCollection idhFiles,
			StringCollection referencedFiles, bool fCreateComments)
		{
			bool fOk = true;
			CodeNamespace codeNamespace = new CodeNamespace();

			// Add additional using statements
			foreach (string ns in usingNamespaces)
				codeNamespace.Imports.Add(new CodeNamespaceImport(ns));

			// Add types from referenced files so that we can resolve types that are not
			// defined in this IDL file.
			foreach (string refFile in referencedFiles)
			{
				CodeNamespace referencedNamespace = DeserializeData(refFile);
				if (referencedNamespace != null)
				{
					foreach (string key in referencedNamespace.UserData.Keys)
						codeNamespace.UserData[key] = referencedNamespace.UserData[key];
				}
			}

			// Load the IDL conversion rules
			if (sXmlFile == null)
			{
				Assembly assembly = Assembly.GetExecutingAssembly();
				sXmlFile = Path.ChangeExtension(assembly.Location, "xml");
			}
			IDLConversions conversions = IDLConversions.Deserialize(sXmlFile);
			conversions.Namespace = codeNamespace;

#if SINGLE_THREADED
			ParseIdhFile(idhFiles);
#else
			m_waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
			ThreadPool.QueueUserWorkItem(new WaitCallback(ParseIdhFiles), idhFiles);
#endif

			using (FileStream stream = new FileStream(sFileName, FileMode.Open, FileAccess.Read))
			{
				IDLLexer lexer = new IDLLexer(stream);
				IDLParser parser = new IDLParser(lexer);
				parser.setFilename(sFileName);

				codeNamespace.Name = sNamespace;
				codeNamespace.Comments.AddRange(AddFileBanner(sFileName, sOutFile));
				codeNamespace.Imports.Add(new CodeNamespaceImport("System"));
				codeNamespace.Imports.Add(new CodeNamespaceImport("System.Runtime.InteropServices"));
				codeNamespace.Imports.Add(new CodeNamespaceImport("System.Runtime.InteropServices.ComTypes"));
				codeNamespace.Imports.Add(new CodeNamespaceImport("System.Runtime.CompilerServices"));

				// And now parse the IDL file
				parser.specification(codeNamespace, conversions);

				// Merge properties
				fOk = MergeProperties(codeNamespace);

				IDLConversions.AdjustReferencesInEnums();

				// Add XML comments
				if (fCreateComments)
				{
					if (m_waitHandle != null)
						m_waitHandle.WaitOne();

					AddComments(codeNamespace.Types);
				}

				// Serialize what we have so that we can re-use later if necessary
				SerializeData(sFileName, codeNamespace);

				// Finally, create the source code
				GenerateCode(sOutFile, codeNamespace);
			}

			return fOk;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Parses the idh files. This is done on a background thread.
		/// </summary>
		/// <param name="obj">List of IDH file names.</param>
		/// ------------------------------------------------------------------------------------
		private void ParseIdhFiles(object obj)
		{
			StringCollection idhFiles = obj as StringCollection;

			// Create IDH processor that will provide the comments from the IDH file
			s_idhProcessor = new IdhCommentProcessor(idhFiles);

			if (m_waitHandle != null)
				m_waitHandle.Set();
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Merges two properties with the same name into one with a get and set method.
		/// </summary>
		/// <param name="codeNamespace">The namespace that defines all classes and methods.</param>
		/// <returns><c>false</c> if a method marked with [propget] comes after a method
		/// with the same name marked as [propput] or [propputref], otherwise <c>true</c>.</returns>
		/// ------------------------------------------------------------------------------------
		private static bool MergeProperties(CodeNamespace codeNamespace)
		{
			bool fOk = true;
			foreach (CodeTypeDeclaration type in codeNamespace.Types)
			{
				for (int i = 0; i < type.Members.Count; i++)
				{
					for (int j = i + 1; j < type.Members.Count; j++)
					{
						if (type.Members[i].Name == type.Members[j].Name
							&& type.Members[i] is CodeMemberProperty
							&& type.Members[j] is CodeMemberProperty)
						{
							CodeMemberProperty first = type.Members[i] as CodeMemberProperty;
							CodeMemberProperty second = type.Members[j] as CodeMemberProperty;
							if (second.HasSet)
							{
								first.HasSet = second.HasSet;
								if (second.UserData.Contains("set_attrs"))
									first.UserData["set_attrs"] = second.UserData["set_attrs"];
							}
							if (second.HasGet)
							{
								// Get needs to come first
								Console.WriteLine("Error: [propget] after [propput/propputref] in {0}.{1}. " +
									"For properties to work in .NET [propget] needs to be defined before [propput].",
									type.Name, first.Name);
								fOk = false;

								first.HasGet = second.HasGet;
								if (second.UserData.Contains("get_attrs"))
									first.UserData["get_attrs"] = second.UserData["get_attrs"];
							}
							type.Members.Remove(second);
						}
					}
				}
			}
			return fOk;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Serializes the data to a file with the same name as the IDL file but with the
		/// extension ".iip".
		/// </summary>
		/// <param name="sFileName">Name of the IDL file.</param>
		/// <param name="cnamespace">The namespace definition with all classes and methods.</param>
		/// ------------------------------------------------------------------------------------
		private static void SerializeData(string sFileName, CodeNamespace cnamespace)
		{
			using (FileStream outFile = new FileStream(Path.ChangeExtension(sFileName, "iip"),
				FileMode.Create))
			{
				BinaryFormatter formatter = new BinaryFormatter();
				try
				{
					formatter.Serialize(outFile, cnamespace);
				}
				catch (SerializationException e)
				{
					Console.WriteLine("Failed to serialize to internal data file. Reason: {0}",
						e.Message);
				}
			}
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Deserializes the data.
		/// </summary>
		/// <param name="sFileName">Name of the IIP file.</param>
		/// <returns>The namespace definition with all classes and methods.</returns>
		/// ------------------------------------------------------------------------------------
		private static CodeNamespace DeserializeData(string sFileName)
		{
			if (!File.Exists(sFileName))
				return null;

			using (FileStream inFile = new FileStream(sFileName, FileMode.Open, FileAccess.Read))
			{
				BinaryFormatter formatter = new BinaryFormatter();
				try
				{
					object obj = formatter.Deserialize(inFile);
					return obj as CodeNamespace;
				}
				catch (SerializationException e)
				{
					Console.WriteLine(
						"Failed to deserialize referenced data from file \"{0}\". Reason: {1}",
						sFileName, e.Message);
				}
			}
			return null;
		}

		/// ------------------------------------------------------------------------------------
		/// <summary>
		/// Generates the code.
		/// </summary>
		/// <param name="sOutFile">The name of the output file (C# file).</param>
		/// <param name="cnamespace">The namespace definition with all classes and methods.</param>
		/// ------------------------------------------------------------------------------------
		private static void GenerateCode(string sOutFile, CodeNamespace cnamespace)
		{
			using (TextWriter textWriter = new StreamWriter(new FileStream(sOutFile, FileMode.Create)))
			{
				CodeGeneratorOptions cgo = new CodeGeneratorOptions();
				cgo.BracingStyle = "C";
				cgo.IndentString = "\t";
				cgo.VerbatimOrder = true;

				CodeDomProvider codeProvider = new CSharpCodeProviderEx();
				codeProvider.GenerateCodeFromNamespace(cnamespace, textWriter, cgo);
			}
		}
	}
}
