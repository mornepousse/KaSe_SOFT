#!/bin/bash

# Script de publication pour KaSe_SOFT
# Usage: ./release.sh <version>
# Exemple: ./release.sh 0.2.3

set -e  # Arrêter en cas d'erreur

# Couleurs pour l'affichage
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

VERSION=$1

if [ -z "$VERSION" ]; then
    echo -e "${RED}Erreur: Veuillez spécifier une version${NC}"
    echo "Usage: $0 <version>"
    echo "Exemple: $0 0.2.3"
    exit 1
fi

# Vérifier que nous sommes dans le bon répertoire
if [ ! -f "KaSe Controller/KaSe Controller.csproj" ]; then
    echo -e "${RED}Erreur: Veuillez exécuter ce script depuis la racine du projet${NC}"
    exit 1
fi

echo -e "${GREEN}=== Publication de KaSe_SOFT v${VERSION} ===${NC}\n"

# 1. Mettre à jour la version dans le .csproj
echo -e "${YELLOW}[1/7] Mise à jour de la version dans le .csproj...${NC}"
sed -i "s/<Version>.*<\/Version>/<Version>${VERSION}<\/Version>/" "KaSe Controller/KaSe Controller.csproj"
sed -i "s/<AssemblyVersion>.*<\/AssemblyVersion>/<AssemblyVersion>${VERSION}.0<\/AssemblyVersion>/" "KaSe Controller/KaSe Controller.csproj"
sed -i "s/<FileVersion>.*<\/FileVersion>/<FileVersion>${VERSION}.0<\/FileVersion>/" "KaSe Controller/KaSe Controller.csproj"
echo -e "${GREEN}✓ Version mise à jour${NC}\n"

# 2. Compiler pour Linux
echo -e "${YELLOW}[2/7] Compilation pour Linux (linux-x64)...${NC}"
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false
echo -e "${GREEN}✓ Compilation Linux terminée${NC}\n"

# 3. Compiler pour Windows
echo -e "${YELLOW}[3/7] Compilation pour Windows (win-x64)...${NC}"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false
echo -e "${GREEN}✓ Compilation Windows terminée${NC}\n"

# 4. Créer les archives
echo -e "${YELLOW}[4/7] Création des archives...${NC}"

# Créer le dossier de sortie s'il n'existe pas
mkdir -p releases

# Archive Linux
echo "  - Création de KaSe_SOFT_linux-x64.zip..."
cd "KaSe Controller/bin/Release/net10.0/linux-x64/publish"
zip -r "../../../../../releases/KaSe_SOFT_linux-x64.zip" . > /dev/null
cd - > /dev/null

# Archive Windows
echo "  - Création de KaSe_SOFT_win-x64.zip..."
cd "KaSe Controller/bin/Release/net10.0/win-x64/publish"
zip -r "../../../../../releases/KaSe_SOFT_win-x64.zip" . > /dev/null
cd - > /dev/null

echo -e "${GREEN}✓ Archives créées dans le dossier 'releases/'${NC}\n"

# 5. Afficher les informations sur les archives
echo -e "${YELLOW}[5/7] Informations sur les archives :${NC}"
ls -lh releases/KaSe_SOFT_*.zip | awk '{print "  - "$9" ("$5")"}'
echo ""

# 6. Créer le tag Git
echo -e "${YELLOW}[6/7] Création du tag Git...${NC}"
read -p "Voulez-vous créer le tag Git v${VERSION} ? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    git add .
    git commit -m "Release v${VERSION}" || echo "Rien à commiter"
    git tag -a "v${VERSION}" -m "Version ${VERSION}"
    echo -e "${GREEN}✓ Tag v${VERSION} créé${NC}\n"
    
    # 7. Push vers GitHub
    echo -e "${YELLOW}[7/7] Push vers GitHub...${NC}"
    read -p "Voulez-vous pusher vers GitHub ? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        git push origin master
        git push origin "v${VERSION}"
        echo -e "${GREEN}✓ Pushed vers GitHub${NC}\n"
    else
        echo -e "${YELLOW}⚠ Push annulé. N'oubliez pas de pusher manuellement :${NC}"
        echo "  git push origin master"
        echo "  git push origin v${VERSION}"
        echo ""
    fi
else
    echo -e "${YELLOW}⚠ Création du tag annulée${NC}\n"
fi

# Instructions finales
echo -e "${GREEN}=== Publication terminée ! ===${NC}\n"
echo -e "${YELLOW}Prochaines étapes :${NC}"
echo "1. Allez sur https://github.com/mornepousse/KaSe_SOFT/releases"
echo "2. Cliquez sur 'Draft a new release'"
echo "3. Sélectionnez le tag v${VERSION}"
echo "4. Titre : KaSe Controller v${VERSION}"
echo "5. Ajoutez les notes de version"
echo "6. Attachez les fichiers :"
echo "   - releases/KaSe_SOFT_linux-x64.zip"
echo "   - releases/KaSe_SOFT_win-x64.zip"
echo "7. Cochez 'Set as the latest release'"
echo "8. Cliquez sur 'Publish release'"
echo ""
echo -e "${GREEN}Les archives sont prêtes dans le dossier 'releases/'${NC}"

