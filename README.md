# Coursia v0.2

Application Windows native et légère pour organiser ses cours et documents.

## Lancer l'application

Dans le dossier du projet :

```powershell
dotnet run
```

Pour produire un executable Windows :

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

L'executable sera dans `bin/Release/net9.0-windows/win-x64/publish/`.

## Installer Coursia

L'installateur Windows est disponible dans `installer-output/Coursia-Setup-v0.2.exe`.
Il crée les raccourcis, installe Coursia dans le profil utilisateur et ajoute la désinstallation Windows.
Les cours et préférences de `%LocalAppData%\\Coursia` sont conservés lors de la désinstallation.

Pour reconstruire l'installateur, installe Inno Setup puis exécute :

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
iscc installer.iss
```

## Fonctions

- Sections de cours dans la barre latérale
- Création de sections et sous-sections
- Import multiple de PDF, Word, PowerPoint, tableurs, images et fichiers texte
- Création de vrais fichiers Word `.docx`, PowerPoint `.pptx` et texte `.txt`
- Choix du dossier de stockage au premier import ou à la première création
- Enregistrement automatique dans le dossier de la matière et de la sous-section active
- Sélecteur visuel de format avec repères Word et PowerPoint
- Réinitialisation complète depuis les paramètres, avec confirmation
- Export d'une sauvegarde complète `.zip` depuis les paramètres
- Copie locale des documents importés pour les retrouver après redémarrage
- Sauvegarde automatique de la bibliothèque dans `%LocalAppData%\\Coursia\\library.json`
- Ouverture des documents avec l'application Windows associée
- Recherche instantanée dans les cours
- Vue des documents récents
- Clic sur la zone vide pour démarrer rapidement
- Glisser-déposer de documents dans la fenêtre
- Raccourcis `Ctrl+N` pour une matière et `Ctrl+O` pour un document
- Tutoriel de découverte au premier lancement
- Paramètres pour changer la couleur d'accent et l'icône affichée
- Icône Coursia personnalisée dans la fenêtre, l'exécutable et l'installateur
- Option pour afficher les extensions des fichiers dans les cartes
- Icône et couleur propres à chaque matière et sous-section
- Clic droit sur une matière pour la renommer ou changer sa personnalisation
- Clic droit sur un document pour l'ouvrir, afficher son emplacement ou le supprimer
- Suppression sécurisée d'une matière avec ses sous-sections et fichiers copiés
- Mode compact pour afficher davantage de matières à l'écran
- Interface native Windows sans serveur web ni navigateur embarqué
- Barre latérale v0.2 avec liste défilante et état vide lisible
- Interface WPF native conservée pour limiter la mémoire et la consommation en arrière-plan
- Démarrage vérifié sur l'exécutable publié v0.2
- Génération PowerPoint corrigée avec une structure Open XML complète
- Indicateur batterie/secteur dans la barre latérale
- Mode économie Coursia activable en un clic, sans modifier les réglages Windows
- Emploi du temps hebdomadaire avec reconnaissance locale des abréviations
- Import du PDF d'emploi du temps, conservé dans le dossier choisi et ouvrable depuis Coursia
- Alerte du prochain cours et rappel de relire le cours
- Notes privées par matière ou sous-section
- Favoris sur les documents
"# coursia" 
