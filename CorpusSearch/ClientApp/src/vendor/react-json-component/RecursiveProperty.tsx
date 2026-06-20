import * as React from 'react';
import ExpandableProperty from './ExpandableProperty';

interface IterableObject {
    [s: number]: number | string | boolean | IterableObject;
}

interface Props {
    property: number | string | boolean | IterableObject;
    propertyName: string;
    excludeBottomBorder: boolean;
    emptyPropertyLabel?: string;
    rootProperty?: boolean;
    propertyNameProcessor?: (name: string) => string;
}

export const camelCaseToNormal = (str: string) =>
    str.replace(/([A-Z])/g, ' $1').replace(/^./, str2 => str2.toUpperCase());

const isLink = (obj: any) => {
    if (typeof obj === 'object' &&
        !Array.isArray(obj) &&
        obj !== null) {
        return "url" in obj && "text" in obj
    }
return false
}

const parseLink = (obj: any): React.ReactNode => {
    return <a rel="noreferrer" target="_blank" href={obj.url}>{obj.text}</a>
}

const RecursiveProperty: React.FC<Props> = ({
    property,
    propertyName,
    excludeBottomBorder = false,
    emptyPropertyLabel = 'Property is empty',
    rootProperty,
    propertyNameProcessor = camelCaseToNormal,
}) => {
    return (
        <div style={
                {
                    paddingTop: 10,
                    paddingLeft: 3,
                    marginLeft: 10,
                    borderBottom: excludeBottomBorder ? '' : '1px solid #b2d6ff',
                    color: "#666",
                    fontSize: 16,
                }
            }>
            {property ? (
                typeof property === 'number' ||
                typeof property === 'string' ||
                typeof property === 'boolean' ||
                isLink(property)
                    ? (
                    <React.Fragment>
                        <span style={{
                            color: "black",
                            fontSize: 14,
                            fontWeight: "bold"
                        }}>
                        {propertyNameProcessor(propertyName)}:
                        </span>
                        {" "}
                        {isLink(property) ? parseLink(property) : property.toString()}
                    </React.Fragment>
                ) : (
                    <ExpandableProperty title={propertyNameProcessor(propertyName)} expanded={!!rootProperty}>
                        {Object.values(property).map((value, index, { length }) => (
                            <RecursiveProperty
                                key={index}
                                property={value}
                                propertyName={Object.getOwnPropertyNames(property)[index]}
                                propertyNameProcessor={propertyNameProcessor}
                                excludeBottomBorder={index === length - 1}
                            />
                        ))}
                    </ExpandableProperty>
                )
            ) : emptyPropertyLabel
            }
        </div>
    );
}

export default RecursiveProperty;