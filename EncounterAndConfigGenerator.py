import json
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from PIL import Image, ImageTk

class LimbusModderApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Stage & EGO JSON Generator")
        self.geometry("1100x750") # Widened to fit the right panel
        
        # --- Main Layout Split ---
        self.main_panes = tk.PanedWindow(self, orient=tk.HORIZONTAL)
        self.main_panes.pack(fill='both', expand=True, padx=10, pady=10)
        
        # Left Panel (Tabs)
        self.left_frame = ttk.Frame(self.main_panes)
        self.main_panes.add(self.left_frame, minsize=600)
        
        # Right Panel (Reference Grid)
        self.right_frame = ttk.Frame(self.main_panes)
        self.main_panes.add(self.right_frame, minsize=400)
        
        # --- Setup Left Side (Tabs) ---
        self.notebook = ttk.Notebook(self.left_frame)
        self.notebook.pack(fill='both', expand=True)
        
        self.stage_tab = ttk.Frame(self.notebook)
        self.ego_tab = ttk.Frame(self.notebook)
        
        self.notebook.add(self.stage_tab, text="Stage Units (12 Draft)")
        self.notebook.add(self.ego_tab, text="EGO Config")
        
        self.unit_entries = []
        self.setup_stage_tab()
        self.setup_ego_tab()

        # --- Setup Right Side (Reference) ---
        self.setup_reference_panel()

    # ==========================================
    # RIGHT PANEL: UNIT REFERENCE
    # ==========================================
    def setup_reference_panel(self):
        tk.Label(self.right_frame, text="Unit Reference Directory", font=('Arial', 12, 'bold')).pack(pady=5)
        
        # Scrollable Canvas setup
        canvas = tk.Canvas(self.right_frame, highlightthickness=0)
        scrollbar = ttk.Scrollbar(self.right_frame, orient="vertical", command=canvas.yview)
        self.scrollable_grid = ttk.Frame(canvas)
        
        self.scrollable_grid.bind(
            "<Configure>",
            lambda e: canvas.configure(scrollregion=canvas.bbox("all"))
        )
        
        canvas.create_window((0, 0), window=self.scrollable_grid, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)
        
        canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")
        
        # Load and populate data
        self.populate_reference_grid()

    def get_unit_data(self):
        """
        TODO: Implement your folder-reading logic here later!
        For now, this returns mock data so the UI works.
        Expects a list of dictionaries: [{"id": "...", "name": "...", "image_path": "..."}, ...]
        """
        return [
            {"id": "2000010101", "name": "LCB Sinner Yi Sang", "image_path": None},
            {"id": "2000010201", "name": "LCB Sinner Faust", "image_path": None},
            {"id": "2000010301", "name": "LCB Sinner Don Quixote", "image_path": None},
            {"id": "2000010401", "name": "LCB Sinner Ryoshu", "image_path": None},
            {"id": "2000010501", "name": "LCB Sinner Meursault", "image_path": None},
            {"id": "2000010601", "name": "LCB Sinner Hong Lu", "image_path": None},
            {"id": "2000010701", "name": "LCB Sinner Heathcliff", "image_path": None},
            {"id": "2000010801", "name": "LCB Sinner Ishmael", "image_path": None},
        ]

    def populate_reference_grid(self):
        unit_data = self.get_unit_data()
        COLUMNS = 2 # Change this to 3 if you want 3 items per row
        
        for index, unit in enumerate(unit_data):
            row = index // COLUMNS
            col = index % COLUMNS
            
            # Card frame for each unit
            card = tk.Frame(self.scrollable_grid, borderwidth=2, relief="groove", padx=5, pady=5)
            card.grid(row=row, column=col, padx=10, pady=10, sticky="nsew")
            
            # 1. Image (Placeholder text if image_path is None)
            # When you add real images later, use tk.PhotoImage(file=unit["image_path"])
            img_lbl = tk.Label(card, text="[ Image Placeholder ]", bg="lightgrey", width=20, height=5)
            img_lbl.pack(pady=2)
            
            # 2. Name
            tk.Label(card, text=unit["name"], font=('Arial', 9, 'bold'), wraplength=130).pack()
            
            # 3. ID
            tk.Label(card, text=f"ID: {unit['id']}", font=('Arial', 9)).pack()
            
            # 4. Copy ID Button for convenience
            copy_btn = tk.Button(card, text="Copy ID", command=lambda u_id=unit['id']: self.copy_to_clipboard(u_id))
            copy_btn.pack(pady=2)

            pil_img = Image.open(unit["image_path"])
            pil_img = pil_img.resize((100, 100)) # Resize to fit the card
            tk_img = ImageTk.PhotoImage(pil_img)

            img_lbl = tk.Label(card, image=tk_img)
            img_lbl.image = tk_img # Essential: keep a reference so it isn't garbage collected
            img_lbl.pack(pady=2)

    def copy_to_clipboard(self, text):
        self.clipboard_clear()
        self.clipboard_append(text)
        self.update() # Keeps the clipboard active

    # ==========================================
    # LEFT PANEL: STAGE TAB
    # ==========================================
    def setup_stage_tab(self):
        headers = ["Unit Type", "Unit ID", "Level", "Count", "Sync Level"]
        for col, text in enumerate(headers):
            tk.Label(self.stage_tab, text=text, font=('Arial', 10, 'bold')).grid(row=0, column=col, padx=5, pady=5)

        for i in range(12):
            row_label = f"Main Unit {i+1}" if i < 6 else f"Sub Unit {i-5}"
            tk.Label(self.stage_tab, text=row_label).grid(row=i+1, column=0, padx=5, pady=2)
            
            default_id = f"200001{(i+1):02d}01"
            
            id_entry = tk.Entry(self.stage_tab, width=15)
            id_entry.insert(0, default_id)
            id_entry.grid(row=i+1, column=1, padx=5, pady=2)
            
            lvl_entry = tk.Entry(self.stage_tab, width=10)
            lvl_entry.insert(0, "60")
            lvl_entry.grid(row=i+1, column=2, padx=5, pady=2)
            
            count_entry = tk.Entry(self.stage_tab, width=10)
            count_entry.insert(0, "1")
            count_entry.grid(row=i+1, column=3, padx=5, pady=2)
            
            sync_entry = tk.Entry(self.stage_tab, width=10)
            sync_entry.insert(0, "4")
            sync_entry.grid(row=i+1, column=4, padx=5, pady=2)
            
            self.unit_entries.append({
                "id": id_entry,
                "lvl": lvl_entry,
                "count": count_entry,
                "sync": sync_entry
            })
            
        btn_frame = tk.Frame(self.stage_tab)
        btn_frame.grid(row=13, column=0, columnspan=5, pady=20)
        
        load_btn = tk.Button(btn_frame, text="Load Stage JSON", command=self.load_stage_json, bg="#ffe066")
        load_btn.pack(side=tk.LEFT, padx=10, ipadx=10, ipady=5)
        
        save_btn = tk.Button(btn_frame, text="Generate Stage JSON", command=self.generate_stage_json, bg="lightblue")
        save_btn.pack(side=tk.LEFT, padx=10, ipadx=10, ipady=5)

    # ==========================================
    # LEFT PANEL: EGO TAB
    # ==========================================
    def setup_ego_tab(self):
        tk.Label(self.ego_tab, text="EGO Configuration (JSON Format)", font=('Arial', 10, 'bold')).pack(pady=10)
        
        self.ego_text = tk.Text(self.ego_tab, wrap="word", height=25, width=65)
        self.ego_text.pack(padx=10, pady=5)
        
        default_ego_json = {
            "EnvyPeccatulumPurpleAura": True,
            "DefaultEgo": [
                [[20101, 4], [20102, 4], [20107, 4], [20108, 4]],
                [[20201, 2], [20205, 3], [20209, 1], [20206, 4]],
                [[20301, 4], [0, 4], [0, 4], [0, 4]],
                [[20401, 4], [0, 4], [0, 4], [0, 4]],
                [[20501, 4], [0, 4], [0, 4], [0, 4]],
                [[20601, 4], [0, 4], [0, 4], [0, 4]],
                [[20701, 4], [0, 4], [0, 4], [0, 4]],
                [[20801, 4], [0, 4], [0, 4], [0, 4]],
                [[20901, 4], [0, 4], [0, 4], [0, 4]],
                [[21001, 4], [0, 4], [0, 4], [0, 4]],
                [[21109, 4], [0, 4], [0, 4], [0, 4]],
                [[21201, 4], [0, 4], [0, 4], [0, 4]]
            ],
            "SpecificIdEgos": []
        }
        self.ego_text.insert(tk.END, json.dumps(default_ego_json, indent=4))
        
        save_ego_btn = tk.Button(self.ego_tab, text="Generate config.json", command=self.generate_ego_json, bg="lightgreen")
        save_ego_btn.pack(pady=10, ipadx=10, ipady=5)

    # ==========================================
    # DATA PROCESSING LOGIC
    # ==========================================
    def load_stage_json(self):
        file_path = filedialog.askopenfilename(filetypes=[("JSON files", "*.json")], title="Select Stage JSON")
        if not file_path:
            return
            
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
                
            wave_list = data.get("waveList", [])
            if not wave_list:
                raise ValueError("No 'waveList' found in the selected JSON.")
                
            wave = wave_list[0]
            units = wave.get("unitList", [])
            sub_units = wave.get("subUnitList", [])
            all_units = units + sub_units
            
            for i, unit_data in enumerate(all_units):
                if i >= 12: 
                    break 
                self.unit_entries[i]["id"].delete(0, tk.END)
                self.unit_entries[i]["id"].insert(0, str(unit_data.get("unitID", "")))
                self.unit_entries[i]["lvl"].delete(0, tk.END)
                self.unit_entries[i]["lvl"].insert(0, str(unit_data.get("unitLevel", "")))
                self.unit_entries[i]["count"].delete(0, tk.END)
                self.unit_entries[i]["count"].insert(0, str(unit_data.get("unitCount", "")))
                self.unit_entries[i]["sync"].delete(0, tk.END)
                self.unit_entries[i]["sync"].insert(0, str(unit_data.get("unitSyncLevel", "")))
                
            messagebox.showinfo("Success", "Stage JSON loaded successfully!")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to load or parse JSON:\n{str(e)}")

    def get_unit_dict(self, index):
        entries = self.unit_entries[index]
        return {
            "unitID": int(entries["id"].get()),
            "unitLevel": int(entries["lvl"].get()),
            "unitCount": int(entries["count"].get()),
            "unitSyncLevel": int(entries["sync"].get()),
            "isHide": False
        }

    def generate_stage_json(self):
        try:
            unit_list = [self.get_unit_dict(i) for i in range(6)]
            sub_unit_list = [self.get_unit_dict(i) for i in range(6, 12)]

            stage_data = {
                "id": 2054133541,
                "stageLevel": 0,
                "stageType": "Abnormality",
                "isBatonPassOn": True,
                "stageEnemyType": "",
                "battleCameraInfo": {"defaultPosX": 0.0, "defaultPosY": 0.0, "defaultPosZ": 50.0},
                "includeBoss": 0,
                "attributeType": "NONE",
                "attackType": "",
                "participantInfo": {"min": 6, "max": 6},
                "waveList": [{
                    "battleMapInfo": {"mapName": "Ab_TiedKing", "mapSize": -1.0},
                    "unitList": unit_list,
                    "subUnitList": sub_unit_list,
                    "supportUnitList": [],
                    "bgmList": ["Battle_Cp9_Boss_1"],
                    "allyPositionID": 89,
                    "enemyPositionID": 90,
                    "useUserSelection": False,
                    "battleCameraWaveInfo": {"defaultPosX": 0.0, "defaultPosY": 0.0, "defaultPosZ": 50.0}
                }],
                "staminaType": "",
                "staminaCost": 0,
                "recommendedLevel": 0,
                "turnLimit": 500,
                "rewardList": [],
                "blockEnemyInfo": False,
                "forceAllyFormation": False,
                "hasGoldenBough": False,
                "hasGoldenBoughGray": False,
                "unlockDanteAbility": False,
                "dangerLevel": 1,
                "lobotomyStageType": 0,
                "libraryOfRuinaStageType": 0,
                "sprName": "",
                "abstainSupporterCharacterIds": [],
                "stageScriptList": ["Refill_Abnormality", "SubUnitDefaultSlotAdder_Abnormality"],
                "eventScriptName": "",
                "abnormalityEventList": []
            }

            file_path = filedialog.asksaveasfilename(defaultextension=".json", initialfile="stage.json", title="Save Stage JSON")
            if file_path:
                with open(file_path, 'w', encoding='utf-8') as f:
                    json.dump(stage_data, f, indent=4)
                messagebox.showinfo("Success", "Stage JSON saved successfully!")
        except ValueError:
            messagebox.showerror("Error", "Please ensure all ID, Level, Count, and Sync fields contain valid numbers.")

    def generate_ego_json(self):
        try:
            ego_data = json.loads(self.ego_text.get("1.0", tk.END))
            file_path = filedialog.asksaveasfilename(defaultextension=".json", initialfile="config.json", title="Save EGO Config JSON")
            if file_path:
                with open(file_path, 'w', encoding='utf-8') as f:
                    json.dump(ego_data, f, indent=4)
                messagebox.showinfo("Success", "config.json saved successfully!")
        except json.JSONDecodeError:
            messagebox.showerror("Error", "Invalid JSON format in the EGO text area.")

if __name__ == "__main__":
    app = LimbusModderApp()
    app.mainloop()