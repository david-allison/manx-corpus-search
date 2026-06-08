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
            "@typescript-eslint/no-shadow": "error",
            "no-shadow": "off",
            "quotes": ["error", "double"],
            "no-unexpected-multiline": "error",
            "semi": ["error", "never", { beforeStatementContinuationChars: "always" }],
            "eqeqeq": "off",

            // typescript-eslint v8 added these to the type-checked set. They flag pre-existing,
            // practically-fine patterns (qs ParsedQs unions stringified, an intentional sentinel
            // literal). Left off to keep this toolchain migration behaviour-neutral — revisit separately.
            "@typescript-eslint/no-base-to-string": "off",
            "@typescript-eslint/no-redundant-type-constituents": "off",
        },
    },
)
