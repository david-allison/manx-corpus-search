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

const RecursiveProperty: React.FC<Props> = props => {
    return (
        <div style={
                {
                    paddingTop: 10,
                    paddingLeft: 3,
                    marginLeft: 10,
                    borderBottom: props.excludeBottomBorder ? '' : 'border-bottom: 1px solid #b2d6ff;',
                    color: "#666",
                    fontSize: 16,
                }
            }>
            {props.property ? (
                typeof props.property === 'number' ||
                typeof props.property === 'string' ||
                typeof props.property === 'boolean' ? (
                    <React.Fragment>
                        <span style={{
                            color: "black",
                            fontSize: 14,
                            fontWeight: "bold"
                        }}>
                        {props.propertyNameProcessor!(props.propertyName)}: 
                        </span>
                        {" "}
                        {props.property.toString()}
                    </React.Fragment>
                ) : (
                    <ExpandableProperty title={props.propertyNameProcessor!(props.propertyName)} expanded={!!props.rootProperty}>
                        {Object.values(props.property).map((property, index, { length }) => (
                            <RecursiveProperty
                                key={index}
                                property={property}
                                propertyName={Object.getOwnPropertyNames(props.property)[index]}
                                propertyNameProcessor={props.propertyNameProcessor}
                                excludeBottomBorder={index === length - 1}
                            />
                        ))}
                    </ExpandableProperty>
                )
            ) : props.emptyPropertyLabel
            }
        </div>
    );
}

RecursiveProperty.defaultProps = {
    emptyPropertyLabel: 'Property is empty',
    excludeBottomBorder: false,
    propertyNameProcessor: camelCaseToNormal
};

export default RecursiveProperty;