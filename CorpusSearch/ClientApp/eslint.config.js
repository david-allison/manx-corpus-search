import tseslint from "typescript-eslint"
import reactHooks from "eslint-plugin-react-hooks"
import globals from "globals"

export default tseslint.config(
    {
        ignores: [
            "build",
            "**/*.d.ts",
            "src/vendor",
            "src/App.test.tsx",
            "src/components/NavMenu.tsx",
            "src/components/Layout.tsx",
        ],
    },
    {
        files: ["src/**/*.{ts,tsx}"],
        extends: [
            ...tseslint.configs.recommended,
            ...tseslint.configs.recommendedTypeChecked,
        ],
        languageOptions: {
            globals: globals.browser,
            parserOptions: {
                project: ["./tsconfig.json"],
                tsconfigRootDir: import.meta.dirname,
            },
        },
        plugins: {
            "react-hooks": reactHooks,
        },
        rules: {
            "react-hooks/rules-of-hooks": "error",
            "react-hooks/exhaustive-deps": "warn",

            // House style (carried over from the old .eslintrc.js)
            // Formatting (quotes, semicolons, indentation) is owned by Prettier
            "@typescript-eslint/no-shadow": "error",
            "no-shadow": "off",
            "eqeqeq": "off",

            // typescript-eslint v8 added no-redundant-type-constituents to the type-checked set;
            // it flags an intentional sentinel literal (To | "historyBack" in BackChevron). Left off.
            "@typescript-eslint/no-redundant-type-constituents": "off",
        },
    },
)
