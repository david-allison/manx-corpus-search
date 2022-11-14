module.exports = {
    "env": {
        "browser": true,
        "es2021": true
    },
    "overrides": [
    {
        files: ['*.ts', '*.tsx'], // Your TypeScript files extension

        // As mentioned in the comments, you should extend TypeScript plugins here,
        // instead of extending them outside the `overrides`.
        // If you don't want to extend any rules, you don't need an `extends` attribute.
        extends: [
            'plugin:@typescript-eslint/recommended',
            'plugin:@typescript-eslint/recommended-requiring-type-checking',
        ],

        parserOptions: {
            project: ['./tsconfig.json'], // Specify it only for TypeScript files
        },
    }],
    "parserOptions": {
        "ecmaVersion": "latest",
        "sourceType": "module"
    },
    "plugins": [
        "react"
    ],
    "rules": {
        '@typescript-eslint/no-shadow': ['error'],
        'no-shadow': 'off',
        'no-undef': 'off',
        "quotes": ["error", "double"],
        "no-unexpected-multiline": "error",
        "semi": ["error", "never", { "beforeStatementContinuationChars": "always" }],
        "eqeqeq": "off",
    },
}
