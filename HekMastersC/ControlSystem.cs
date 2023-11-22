using System;
using Crestron.SimplSharp;                          	// For Basic SIMPL# Classes
using Crestron.SimplSharpPro;                       	// For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro.Diagnostics;		    	// For System Monitor Access
using Crestron.SimplSharpPro.DeviceSupport;         	// For Generic Device Support
using Crestron.SimplSharpPro.UI;                        // X panel
using MastersHelperLibrary;                            // Custom Library
 
namespace HekMastersC
{
    public class ControlSystem : CrestronControlSystem
    {
        //Containers that are Global to this class
        XpanelForSmartGraphics myXpanel;    //TP container

        TCPClientHelper myClient;           //This is conainer for my TCP client class

        /// <summary>
        /// ControlSystem Constructor. Starting point for the SIMPL#Pro program.
        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// 
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// 
        /// You cannot send / receive data in the constructor
        /// </summary>
        public ControlSystem()  //Default Constructor
            : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                //Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(_ControllerEthernetEventHandler);
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in the constructor: {0}", e.Message);
            }
        }

        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// 
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem()
        {
            try
            {

                CrestronConsole.Print("yo dude");
                //VirtualConsole.Start(40000);  //Start Virtual console

                myXpanel = new XpanelForSmartGraphics(0x03, this);
                if (myXpanel.Register() == eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    myXpanel.SigChange += MyXpanel_SigChange;
                }
                else
                {
                    ErrorLog.Error("TP at ID {0} unable to register.", myXpanel.ID);
                }
                //TCP class
                myClient = new TCPClientHelper("127.0.0.1", 55555);
                myClient.tcpHelperEvent += MyClient_tcpHelperEvent;
            }
            catch (Exception e)
            {
                ErrorLog.Error("Error in InitializeSystem: {0}", e.Message);
            }
        }

        private void MyClient_tcpHelperEvent(object sender, TCPClientHelperEventArgs e)
        {
            if(e.Message == "STATUS")  // is this a status change?
            {
                myXpanel.BooleanInput[20].BoolValue = e.Connected;
                myXpanel.BooleanInput[21].BoolValue = !e.Connected;
            }

            else if(e.Message == "RX")  // We received data
            {
                //VirtualConsole.Send(string.Format(" RX={0}" , e.RX));
                myXpanel.StringInput[2].StringValue = e.RX;
                
                if (e.RX.Contains("PON"))  //does strng contain PON
                {
                    myXpanel.BooleanInput[22].BoolValue = true;
                    myXpanel.BooleanInput[23].BoolValue = false;
                }
                if (e.RX.Contains("POF"))  //does strng contain POF
                {
                    myXpanel.BooleanInput[23].BoolValue = true;
                    myXpanel.BooleanInput[22].BoolValue = false;
                }

                else if (e.RX.Contains("LH?"))     //did we get a lamp hours response?
                {
                    var hours = e.RX.TrimStart('\x02').TrimEnd('\x03');   //clean up response
                    hours = hours.Substring(hours.IndexOf('?') + 1);      //skip over the ? mark by adding 1
                    myXpanel.StringInput[3].StringValue = hours;
                }
            }
        }

        



        private void MyXpanel_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
           // VirtualConsole.Send(string.Format("Xpanel Join#{0} is {1}", args.Sig.Number, args.Sig.BoolValue));
            if (args.Sig.Type == eSigType.Bool) // is this digital?
            {
                //Momentary
                if (args.Sig.Number == 11) // Needs to be outside the if below
                {
                    myXpanel.BooleanInput[11].BoolValue = args.Sig.BoolValue; //Take the event value and apply to fb
                }

                if(args.Sig.BoolValue == true) // true is a press
                {
                    switch(args.Sig.Number)
                    {
                        case 1: // DJ 1 was pressed
                            myXpanel.BooleanInput[1].Pulse();
                            myXpanel.StringInput[1].StringValue = "Home MF Page";
                            break;
                        case 2:
                            myXpanel.BooleanInput[2].Pulse();
                            myXpanel.StringInput[1].StringValue = "Projector Page";
                            break;
                        case 3:
                            myXpanel.BooleanInput[3].Pulse();
                            myXpanel.StringInput[1].StringValue = "Phonebook";
                            break;
                            //Toggle
                        case 10:
                            myXpanel.BooleanInput[10].BoolValue = !myXpanel.BooleanInput[10].BoolValue;  //not or opposite
                            break;
                            //Interlock
                        case 12:
                            myXpanel.BooleanInput[14].BoolValue = false;
                            myXpanel.BooleanInput[13].BoolValue = false;
                            myXpanel.BooleanInput[12].BoolValue = true;
                            break;
                        case 13:
                            myXpanel.BooleanInput[14].BoolValue = false;
                            myXpanel.BooleanInput[13].BoolValue = true;
                            myXpanel.BooleanInput[12].BoolValue = false;
                            break;
                        case 14:
                            myXpanel.BooleanInput[14].BoolValue = true;
                            myXpanel.BooleanInput[13].BoolValue = false;
                            myXpanel.BooleanInput[12].BoolValue = false;
                            break;
                    }

                    //Projector buttons

                    switch(args.Sig.Number)
                    {
                        case 20:
                            myClient.Connect();
                            break;
                        case 21:
                            myClient.Disconnect();
                            break;
                        case 22:
                            myClient.TX = "\x02\x01\x00\x00PON\x00\x00\x00\x03";  

                            break;
                        case 23:
                            myClient.TX = "\x02\x01\x00\x00POFF\x00\x00\x00\x03";
                            break;
                        case 24:
                            myClient.TX = "\x02\x01\x00\x00LH?\x00\x00\x00\x03";
                            break;
                    }
                }



            }
        }

        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// wich Ethernet adapter this event belongs to.
        /// </param>
        void _ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        //
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {

                    }
                    break;
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType"></param>
        void _ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    //The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Resumed):
                    //The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case (eProgramStatusEventType.Stopping):
                    //The program has been stopped.
                    //Close all threads. 
                    //Shutdown all Client/Servers in the system.
                    //General cleanup.
                    //Unsubscribe to all System Monitor events
                    break;
            }

        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType"></param>
        void _ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to preform clean up and save any settings to disk.
                    break;
            }

        }
    }
}