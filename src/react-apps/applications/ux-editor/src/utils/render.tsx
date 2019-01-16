import { Typography } from '@material-ui/core';
import React = require('react');
import Select from 'react-select';
import CreatableSelect from 'react-select/lib/Creatable';
import { customInput } from '../components/config/EditModalContent';
import { SelectDataModelComponent } from '../components/config/SelectDataModelComponent';
import { formatCreateTextLabel, getTextResource, truncate } from './language';
import { DefaultDataModelBindingKey } from '../components/FormComponent';

export const styles = {
  inputHelper: {
    marginTop: '2.4rem',
    fontSize: '1.6rem',
    lineHeight: '3.2rem',
  },
};

export function renderPropertyLabel(textKey: string) {
  return (
    <Typography style={styles.inputHelper}>
      {textKey}
    </Typography>
  );
}

export function noOptionsMessage(language: any): string {
  return language.general.no_options;
}

export function renderSelectDataModelBinding(
  dataModelBinding: IDataModelBindings = {},
  onDataModelChange: any,
  language: any,
  label?: string,
  returnValue?: any,
  key: string = DefaultDataModelBindingKey,
): JSX.Element {
  return (
    <div>
      {renderPropertyLabel(label ?
        language.ux_editor.modal_properties_data_model_helper + ' ' + language.general.for + ' ' + label :
        language.ux_editor.modal_properties_data_model_helper)
      }
      <SelectDataModelComponent
        selectedElement={dataModelBinding[key]}
        // tslint:disable-next-line:jsx-no-lambda
        onDataModelChange={(dataModelField) => onDataModelChange(dataModelField, returnValue)}
        language={language}
        // tslint:disable-next-line:jsx-no-lambda
        noOptionsMessage={() => noOptionsMessage(language)}
      />
    </div>
  );
}

export function renderSelectTextFromResources(
  labelText: string,
  onChangeFunction: (e: any) => void,
  textResources: ITextResource[],
  language: any,
  placeholder?: string,
  truncateLimit: number = 80,
  createNewTextAllowed: boolean = false,
): JSX.Element {
  const resources: any = [];
  if (textResources) {
    textResources.forEach((textResource: any) => {
      const option = truncate(textResource.value, truncateLimit);
      resources.push({ value: textResource.id, label: option.concat('\n(', textResource.id, ')') });
    });
  }
  return (
    <div>
      {renderPropertyLabel(language.ux_editor[labelText])}
      {createNewTextAllowed ?
        <CreatableSelect
          styles={customInput}
          options={resources}
          defaultValue={''}
          onChange={onChangeFunction}
          isClearable={true}
          placeholder={placeholder ?
            truncate(getTextResource(placeholder, textResources), 40)
            : language.general.search}
          // tslint:disable-next-line:jsx-no-lambda
          formatCreateLabel={(inputValue: string) => formatCreateTextLabel(inputValue, language)}
          // tslint:disable-next-line:jsx-no-lambda
          noOptionsMessage={() => noOptionsMessage(language)}
        />
        :
        <Select
          styles={customInput}
          options={resources}
          defaultValue={''}
          onChange={onChangeFunction}
          isClearable={true}
          placeholder={placeholder ?
            truncate(getTextResource(placeholder, textResources), 40)
            : language.general.search}
          // tslint:disable-next-line:jsx-no-lambda
          noOptionsMessage={() => noOptionsMessage(language)}
        />
      }
    </div>
  );
}
