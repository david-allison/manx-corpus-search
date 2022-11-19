curl -sL https://api.github.com/repos/david-allison/cregeen-manx-dictionary-data/releases/latest | jq -r '.assets[].browser_download_url' | xargs -n 1 curl -o cregeen.json -sSL
echo 'downloaded'
