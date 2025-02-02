using System;
using Extensibility;
using EnvDTE;
using EnvDTE80;
namespace FwAddin
{
	/// <summary>The object for implementing an Add-in.</summary>
	/// <seealso class='IDTExtensibility2' />
	public class Connect : IDTExtensibility2, IDTCommandTarget
	{
		private DTE2 m_applicationObject;
		private AddIn m_addInInstance;
		private AddinCommands m_commands;

		/// <summary>Implements the constructor for the Add-in object. Place your initialization code within this method.</summary>
		public Connect()
		{
		}

		/// <summary>Implements the OnConnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being loaded.</summary>
		/// <param term='application'>Root object of the host application.</param>
		/// <param term='connectMode'>Describes how the Add-in is being loaded.</param>
		/// <param term='addInInst'>Object representing this Add-in.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnConnection(object application, ext_ConnectMode connectMode, 
			object addInInst, ref Array custom)
		{
			m_applicationObject = (DTE2)application;
			m_addInInstance = (AddIn)addInInst;
			
			//if (connectMode == ext_ConnectMode.ext_cm_UISetup)
			{
				object[] contextGUIDS = new object[] { };

				try
				{
					//Add a command to the Commands collection:
					object[] contextUIGUIDs = new object[0];
					m_applicationObject.Commands.AddNamedCommand(m_addInInstance,
						"GotoFunctionHeaderDown", "GotoFunctionHeaderDown", 
						"Moves cursor to the next function definition.", true, 0, ref contextUIGUIDs, 
						(int)vsCommandStatus.vsCommandStatusSupported + 
						(int)vsCommandStatus.vsCommandStatusEnabled);
				}
				catch (System.ArgumentException)
				{
					//If we are here, then the exception is probably because a command with that name
					//  already exists. If so there is no need to recreate the command and we can 
					//  safely ignore the exception.
				}
				try
				{
					//Add a command to the Commands collection:
					object[] contextUIGUIDs = new object[0];
					m_applicationObject.Commands.AddNamedCommand(m_addInInstance,
						"GotoFunctionHeaderUp", "GotoFunctionHeaderUp",
						"Moves cursor to the previous function definition.", true, 0, ref contextUIGUIDs,
						(int)vsCommandStatus.vsCommandStatusSupported +
						(int)vsCommandStatus.vsCommandStatusEnabled);
				}
				catch (System.ArgumentException)
				{
				}
				try
				{
					//Add a command to the Commands collection:
					object[] contextUIGUIDs = new object[0];
					m_applicationObject.Commands.AddNamedCommand(m_addInInstance,
						"InsertMethodHeader", "InsertMethodHeader",
						"Insert a class or method XML tag block sandwiched between a set of dashed lines.", 
						true, 0, ref contextUIGUIDs,
						(int)vsCommandStatus.vsCommandStatusSupported +
						(int)vsCommandStatus.vsCommandStatusEnabled);
				}
				catch (System.ArgumentException)
				{
				}
				try
				{
					//Add a command to the Commands collection:
					object[] contextUIGUIDs = new object[0];
					m_applicationObject.Commands.AddNamedCommand(m_addInInstance,
						"ToggleHAndCpp", "ToggleHAndCpp",
						"Toggle between the .H and .CPP files.",
						true, 0, ref contextUIGUIDs,
						(int)vsCommandStatus.vsCommandStatusSupported +
						(int)vsCommandStatus.vsCommandStatusEnabled);
				}
				catch (System.ArgumentException)
				{
				}
			}

			m_commands = new AddinCommands(m_applicationObject);
		}

		/// <summary>Implements the OnDisconnection method of the IDTExtensibility2 interface. Receives notification that the Add-in is being unloaded.</summary>
		/// <param term='disconnectMode'>Describes how the Add-in is being unloaded.</param>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnDisconnection(ext_DisconnectMode disconnectMode, ref Array custom)
		{
		}

		/// <summary>Implements the OnAddInsUpdate method of the IDTExtensibility2 interface. Receives notification when the collection of Add-ins has changed.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />		
		public void OnAddInsUpdate(ref Array custom)
		{
		}

		/// <summary>Implements the OnStartupComplete method of the IDTExtensibility2 interface. Receives notification that the host application has completed loading.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnStartupComplete(ref Array custom)
		{
		}

		/// <summary>Implements the OnBeginShutdown method of the IDTExtensibility2 interface. Receives notification that the host application is being unloaded.</summary>
		/// <param term='custom'>Array of parameters that are host application specific.</param>
		/// <seealso class='IDTExtensibility2' />
		public void OnBeginShutdown(ref Array custom)
		{
		}

		#region IDTCommandTarget Members

		/// <summary>Implements the QueryStatus method of the IDTCommandTarget interface. This 
		/// is called when the command's availability is updated</summary>
		/// <param term='commandName'>The name of the command to determine state for.</param>
		/// <param term='neededText'>Text that is needed for the command.</param>
		/// <param term='status'>The state of the command in the user interface.</param>
		/// <param term='commandText'>Text requested by the neededText parameter.</param>
		/// <seealso class='Exec' />
		public void QueryStatus(string commandName, vsCommandStatusTextWanted neededText, 
			ref vsCommandStatus status, ref object commandText)
		{
			if (neededText == vsCommandStatusTextWanted.vsCommandStatusTextWantedNone)
			{
				if (commandName == "FwAddin.Connect.GotoFunctionHeaderDown" ||
					commandName == "FwAddin.Connect.GotoFunctionHeaderUp" ||
					commandName == "FwAddin.Connect.InsertMethodHeader" ||
					commandName == "FwAddin.Connect.ToggleHAndCpp")
				{
					status = (vsCommandStatus)vsCommandStatus.vsCommandStatusSupported | 
						vsCommandStatus.vsCommandStatusEnabled;
					return;
				}
			}
		}

		/// <summary>Implements the Exec method of the IDTCommandTarget interface. This is called 
		/// when the command is invoked.</summary>
		/// <param term='commandName'>The name of the command to execute.</param>
		/// <param term='executeOption'>Describes how the command should be run.</param>
		/// <param term='varIn'>Parameters passed from the caller to the command handler.</param>
		/// <param term='varOut'>Parameters passed from the command handler to the caller.</param>
		/// <param term='handled'>Informs the caller if the command was handled or not.</param>
		/// <seealso class='Exec' />
		public void Exec(string commandName, vsCommandExecOption executeOption, ref object varIn, 
			ref object varOut, ref bool handled)
		{
			handled = false;
			if (executeOption == vsCommandExecOption.vsCommandExecOptionDoDefault)
			{
				if (commandName == "FwAddin.Connect.GotoFunctionHeaderDown")
				{
					m_commands.GotoFunctionHeaderDown();
					handled = true;
					return;
				}
				else if (commandName == "FwAddin.Connect.GotoFunctionHeaderUp")
				{
					m_commands.GotoFunctionHeaderUp();
					handled = true;
					return;
				}
				else if (commandName == "FwAddin.Connect.InsertMethodHeader")
				{
					m_commands.InsertMethodHeader();
					handled = true;
					return;
				}
				else if (commandName == "FwAddin.Connect.ToggleHAndCpp")
				{
					m_commands.ToggleHAndCpp();
					handled = true;
					return;
				}
			}
		}

		#endregion
	}
}