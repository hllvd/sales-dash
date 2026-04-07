# Importing Process - Contracts and Users

The process of importing contracts from the Import Wizard involves several sequential steps that ensure data integrity and proper mapping between contracts and users. 

## Step 1: Initial Import
- **Action**: Upload the base file (usually an `.xlsx` or `.csv` file, for example, "modelo_referencia_retencao").
- **Description**: The system reads the historical contract data and validates its structure. This initiates the wizard session and prepares the data for extraction.

## Step 2: Users Extraction and Completion
- **Download**: The system extracts unique users (vendedores) and their IDs (*matrículas*) from the initial file and generates a template file named `users.csv`.
- **Manual Action**: You must download this `users.csv` and appropriately fill out mandatory fields such as **Email**, **ParentEmail** (superior's email), and **Owner_Matricula** (1 for yes, 0 for no).
- **Import**: Re-upload the newly completed `users.csv` into the wizard (Step 2 Import).
- **Processing**: The system processes this file to dynamically import the vendors (Users), their associated Matrículas, Points of Sale (PVs), and create their hierarchy (Groups).

## Step 3: Contracts Enrichment
- **Action**: Download the finalized `contracts.csv` file from the Wizard.
- **Description**: The system combines the original contract data with the populated `users.csv`, enriching the original data by appending each user's **Email**. It also automatically normalizes contract statuses (e.g., from *conferencia* text like "NORMAL" or "EXCLUIDO" to system states like "Active" or "Defaulted").

## Final Step: Mapping and Contracts Import
- **Action**: Take the `contracts.csv` file generated in Step 3 and upload it in the regular Import Mapping screen.
- **Execution**: Map the columns to the corresponding backend system fields and confirm the import. This will persist all contracts into the database.

---

### FAQ

**Q: Should we assign contracts for their users given the user's email?**

**A: Yes.** The entire reason the wizard generates and processes the `users.csv` is to resolve the correct `Email` for each user/matrícula combination. In Step 3, the exported `contracts.csv` contains this resolved email. During the final import mapping process, you must map the file's `Email` column to the `UserEmail` system field. The backend relies strictly on this email to find the matching user and assign the contract to them.
