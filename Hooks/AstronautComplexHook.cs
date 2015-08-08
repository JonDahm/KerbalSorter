﻿using KSP;
using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

using StockList = KerbalSorter.Hooks.Utilities.StockList;

namespace KerbalSorter.Hooks {
    /// <summary>
    /// The main hook for the Astronaut Complex. Started up whenever the Space Centre is loaded.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class AstronautComplexHook : MonoBehaviour {
        CMAstronautComplex complex;
        SortBar sortBarCrew;
        SortBar sortBarApplicants;

        bool sortBarCrewDisabled = false;
        bool sortBarApplicantsDisabled = false;

        StockRoster available;
        StockRoster assigned;
        StockRoster killed;
        StockRoster applicants;
        CrewPanel curPanel;

        /// <summary>
        /// Set up the SortBars for the Astronaut Complex. (Callback)
        /// </summary>
        protected void Start() {
            try {
                // Set up hooks:
                GameEvents.onGUIAstronautComplexSpawn.Add(OnACSpawn);
                GameEvents.onGUIAstronautComplexDespawn.Add(OnACDespawn);
                GameEvents.OnCrewmemberHired.Add(OnHire);
                GameEvents.OnCrewmemberSacked.Add(OnFire);

                // Get rosters:
                complex = UIManager.instance.gameObject.GetComponentsInChildren<CMAstronautComplex>(true).FirstOrDefault();
                if( complex == null ) throw new Exception("Could not find astronaut complex");
                UIScrollList availableList = complex.transform.Find("CrewPanels/panel_enlisted/panelManager/panel_available/scrolllist_available").GetComponent<UIScrollList>();
                UIScrollList assignedList = complex.transform.Find("CrewPanels/panel_enlisted/panelManager/panel_assigned/scrolllist_assigned").GetComponent<UIScrollList>();
                UIScrollList killedList = complex.transform.Find("CrewPanels/panel_enlisted/panelManager/panel_kia/scrolllist_kia").GetComponent<UIScrollList>();
                UIScrollList applicantList = complex.transform.Find("CrewPanels/panel_applicants/scrolllist_applicants").GetComponent<UIScrollList>();
                available = new StockRoster(availableList);
                assigned = new StockRoster(assignedList);
                killed = new StockRoster(killedList);
                applicants = new StockRoster(applicantList);

                // Get sort bar definitions:
                SortBarDef barAvailable  = ButtonAndBarLoader.SortBarDefs[Utilities.GetListName(StockList.Available)];
                SortBarDef barApplicants = ButtonAndBarLoader.SortBarDefs[Utilities.GetListName(StockList.Applicants)];

                // Initialize the crew sort bar:
                curPanel = CrewPanel.Available;
                sortBarCrew = gameObject.AddComponent<SortBar>();
                sortBarCrew.SetDefinition(barAvailable);
                sortBarCrew.SetSortDelegate(available.Sort);
                sortBarCrew.StateChanged += CrewSortBarStateChanged;
                sortBarCrew.enabled = false;
                sortBarCrewDisabled = available == null
                                   || barAvailable.buttons == null
                                   || barAvailable.buttons.Length == 0;

                /// Initialize the applicant sort bar:
                sortBarApplicants = gameObject.AddComponent<SortBar>();
                sortBarApplicants.SetDefinition(barApplicants);
                sortBarApplicants.SetSortDelegate(applicants.Sort);
                sortBarApplicants.StateChanged += AppSortBarStateChanged;
                sortBarApplicants.enabled = false;
                sortBarApplicantsDisabled = applicants == null
                                         || barApplicants.buttons == null
                                         || barApplicants.buttons.Length == 0;


                // Assign enable listeners to the rosters:
                Utilities.AddOnEnableListener(availableList.gameObject, OnTabAvailable, true);
                Utilities.AddOnEnableListener(assignedList.gameObject, OnTabAssigned, true);
                Utilities.AddOnEnableListener(killedList.gameObject, OnTabKilled, true);

                // There's no other way to detect KSI's presence, unfortunately. :/
                foreach( AssemblyLoader.LoadedAssembly asm in AssemblyLoader.loadedAssemblies ) {
                    if( asm.dllName == "KSI" ) {
                        sortBarApplicantsDisabled = true;
                    }
                }
            }
            catch( Exception e ) {
                Debug.LogError("KerbalSorter: Unexpected error in AstronautComplexHook: " + e);
            }
        }


        /// <summary>
        /// Set the SortBars' position and enable them on Astronaut Complex spawn. (Callback)
        /// </summary>
        protected void OnACSpawn() {
            try {
                // Set position:
                Transform targetTabTrans = complex.transform.Find("CrewPanels/panel_enlisted/tabs/tab_kia");
                BTPanelTab targetTab = targetTabTrans.GetComponent<BTPanelTab>();
                Vector3 screenPos = Utilities.GetPosition(targetTabTrans);
                float x = screenPos.x + targetTab.width + 5;
                float y = screenPos.y - 1;
                sortBarCrew.SetPos(x, y);
                sortBarCrew.enabled = !sortBarCrewDisabled;

                string name = GetSortBarName(curPanel);
                if( KerbalSorterStates.IsSortBarStateStored(name) ) {
                    sortBarCrew.SetState(KerbalSorterStates.GetSortBarState(name));
                }

                if( !sortBarApplicantsDisabled ) {
                    targetTabTrans = complex.transform.Find("CrewPanels/panel_applicants/tab_crew");
                    BTButton targetTab2 = targetTabTrans.GetComponent<BTButton>(); // Because consistancy is not their strong suit.
                    screenPos = Utilities.GetPosition(targetTabTrans);
                    x = screenPos.x + targetTab2.width + 5;
                    y = screenPos.y - 1;
                    sortBarApplicants.SetPos(x, y);
                    sortBarApplicants.enabled = true;

                    name = Utilities.GetListName(StockList.Applicants);
                    if( KerbalSorterStates.IsSortBarStateStored(name) ) {
                        sortBarApplicants.SetState(KerbalSorterStates.GetSortBarState(name));
                    }
                }
            }
            catch( Exception e ) {
                Debug.LogError("KerbalSorter: Unexpected error in AstronautComplexHook: " + e);
            }
        }

        /// <summary>
        /// Disable the SortBars on Astronaut Complex despawn. (Callback)
        /// </summary>
        protected void OnACDespawn() {
            sortBarCrew.enabled = false;
            sortBarApplicants.enabled = false;
        }

        /// <summary>
        /// Re-sort the crew lists when a new kerbal is hired. (Callback)
        /// </summary>
        /// <param name="kerbal">The kerbal just hired</param>
        /// <param name="numActiveKerbals">The new number of active kerbals</param>
        protected void OnHire(ProtoCrewMember kerbal, int numActiveKerbals) {
            try {
                sortBarCrew.SortRoster(true);
            }
            catch( Exception e ) {
                Debug.LogError("KerbalSorter: Unexpected error in AstronautComplexHook: " + e);
            }
        }

        /// <summary>
        /// Re-sort the applicant list when a kerbal is fired.
        /// </summary>
        /// <param name="kerbal">The kerbal just fired</param>
        /// <param name="numActiveKerbals">The new number of active kerbals</param>
        protected void OnFire(ProtoCrewMember kerbal, int numActiveKerbals) {
            try {
                sortBarApplicants.SortRoster(true);
            }
            catch( Exception e ) {
                Debug.LogError("KerbalSorter: Unexpected error in AstronautComplexHook: " + e);
            }
        }

        /// <summary>
        /// Switch the list that the crew Sort Bar operates with on tab change. (Callback)
        /// </summary>
        /// <param name="panel">The new panel</param>
        protected void OnTabSwitch(CrewPanel panel) {
            try {
                if( this.curPanel == panel ) {
                    return;
                }
                this.curPanel = panel;

                string name = GetSortBarName(panel);
                SortBar.SortDelegate sorter = null;
                switch( panel ) {
                    case CrewPanel.Available:
                        sorter = this.available.Sort;
                        break;
                    case CrewPanel.Assigned:
                        sorter = this.assigned.Sort;
                        break;
                    case CrewPanel.Killed:
                        sorter = this.killed.Sort;
                        break;
                }

                SortBarDef def = ButtonAndBarLoader.SortBarDefs[name];
                sortBarCrew.SetDefinition(def);
                sortBarCrew.SetSortDelegate(sorter);
                if( KerbalSorterStates.IsSortBarStateStored(name) ) {
                    sortBarCrew.SetState(KerbalSorterStates.GetSortBarState(name));
                }

                sortBarCrewDisabled = sorter == null || def.buttons == null || def.buttons.Length == 0;
                sortBarCrew.enabled = !sortBarCrewDisabled;
            }
            catch( Exception e ) {
                Debug.LogError("KerbalSorter: Unexpected error in AstronautComplexHook: " + e);
            }
        }

        /// <summary>
        /// Switch the crew Sort Bar to operate on the Available list. (Callback)
        /// </summary>
        protected void OnTabAvailable() {
            OnTabSwitch(CrewPanel.Available);
        }

        /// <summary>
        /// Switch the crew Sort Bar to operate on the Assigned list. (Callback)
        /// </summary>
        protected void OnTabAssigned() {
            OnTabSwitch(CrewPanel.Assigned);
        }

        /// <summary>
        /// Switch the crew Sort Bar to operate on the Killed list. (Callback)
        /// </summary>
        protected void OnTabKilled() {
            OnTabSwitch(CrewPanel.Killed);
        }

        /// <summary>
        /// Save the applicant Sort Bar's state whenever it changes. (Callback)
        /// </summary>
        /// <param name="bar">The applicant Sort Bar</param>
        /// <param name="newState">The new state of the Sort Bar</param>
        protected void AppSortBarStateChanged(SortBar bar, SortBarState newState) {
            try {
                KerbalSorterStates.SetSortBarState(Utilities.GetListName(StockList.Applicants), newState);
            }
            catch( Exception e ) {
                Debug.LogError("KerbalSorter: Unexpected error in AstronautComplexHook: " + e);
            }
        }

        /// <summary>
        /// Save the crew Sort Bar's state whenever it changes. (Callback)
        /// </summary>
        /// <param name="bar">The crew Sort Bar</param>
        /// <param name="newState">The new state of the Sort Bar</param>
        protected void CrewSortBarStateChanged(SortBar bar, SortBarState newState) {
            try {
                string name = GetSortBarName(curPanel);
                KerbalSorterStates.SetSortBarState(name, newState);
            }
            catch( Exception e ) {
                Debug.LogError("KerbalSorter: Unexpected error in AstronautComplexHook: " + e);
            }
        }

        protected string GetSortBarName(CrewPanel panel) {
            switch( panel ) {
                case CrewPanel.Available:
                    return Utilities.GetListName(StockList.Available);
                case CrewPanel.Assigned:
                    return Utilities.GetListName(StockList.Assigned);
                case CrewPanel.Killed:
                    return Utilities.GetListName(StockList.Killed);
            }
            return "";
        }

        /// <summary>
        /// Remove GameEvent hooks when this hook is unloaded. (Callback)
        /// </summary>
        protected void OnDestroy() {
            try {
                GameEvents.onGUIAstronautComplexSpawn.Remove(OnACSpawn);
                GameEvents.onGUIAstronautComplexDespawn.Remove(OnACDespawn);
                GameEvents.OnCrewmemberHired.Remove(OnHire);
                GameEvents.OnCrewmemberSacked.Remove(OnFire);
            }
            catch( Exception e ) {
                Debug.LogError("KerbalSorter: Unexpected error in AstronautComplexHook: " + e);
            }
        }


        /// <summary>
        /// The panels in the Astronaut Complex's tab control.
        /// </summary>
        protected enum CrewPanel {
            Available,
            Assigned,
            Killed
        }
    }

    /// <summary>
    /// A hook for the Astronaut Complex accessed through the editors.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class AstronautComplexHook_EditorFix : AstronautComplexHook {
    }
}
