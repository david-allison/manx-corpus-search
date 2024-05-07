echo 'Downloading Kelly Manx to English...'
curl -sL https://api.github.com/repos/david-allison/kelly-m2e-manx-dictionary-data/releases/latest | jq -r '.assets[].browser_download_url' | xargs -n 1 curl -o kellym2e.json -sSL

