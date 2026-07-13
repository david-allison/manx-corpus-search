# permissions are required for git clone

# setup /OpenData
cd /var/corpus-search/open-data
if cd manx-search-data; then git pull; else git clone https://github.com/david-allison/manx-search-data.git ; fi

# Generate the list of recent changes
cd /var/corpus-search/open-data/manx-search-data
/app/newdocs > /var/corpus-search/open-data/manx-search-data/OpenData/newdocs.txt

# setup /ClosedData
cd /var/corpus-search/closed-data
if cd corpus-search-data-private; then git pull; else git clone https://github.com/Manx-Language-Toolkit/corpus-search-data-private.git ; fi

# setup dictionaries
cd /var/corpus-search/dictionaries
/app/tools/download_cregeen.sh
/app/tools/download_kelly_m2e.sh

echo "Copying dictionaries to /Resources"
cp * /app/Resources

# setup the lemma table: prod tracks manx-lemma-data HEAD (the daily restart
# picks up merges); the image's baked copy remains as the fallback
cd /var/corpus-search/lemma-data
if cd manx-lemma-data; then git pull; else git clone https://github.com/david-allison/manx-lemma-data.git ; fi
cp /var/corpus-search/lemma-data/manx-lemma-data/cregeen.tsv /app/Resources
# the disambiguation layers (LemmaResolver) are optional until adopted upstream
for f in names.tsv spoken.json lemma.overrides.tsv lemma.sidecar.tsv; do
    if [ -f "/var/corpus-search/lemma-data/manx-lemma-data/$f" ]; then
        cp "/var/corpus-search/lemma-data/manx-lemma-data/$f" /app/Resources
    fi
done


# do better here
rm -rf /app/OpenData/{*,.*}
rm -rf /app/ClosedData/{*,.*}

cp -r /var/corpus-search/open-data/manx-search-data/OpenData /app
cp -r /var/corpus-search/closed-data/corpus-search-data-private/ClosedData /app


# we've created the folder, no longer a need for root
su - $(id -un $APP_UID)

cd /app

dotnet CorpusSearch.dll