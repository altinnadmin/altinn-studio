import { createStyles, IconButton, Grid, Theme, withStyles, Drawer } from '@material-ui/core';
import classNames = require('classnames');
import * as React from 'react';
import { DragDropContext } from 'react-dnd';
import HTML5Backend from 'react-dnd-html5-backend';
import { connect } from 'react-redux';
import FileEditor from '../../../shared/src/file-editor/FileEditor';
import { ServiceLogicMenu } from '../../../shared/src/navigation/drawer/rightDrawerMenu';
import altinnTheme from '../../../shared/src/theme/altinnStudioTheme';
import VersionControlHeader from '../../../shared/src/version-control/versionControlHeader';
import AppDataActionDispatcher from '../actions/appDataActions/appDataActionDispatcher';
import FormDesignerActionDispatchers from '../actions/formDesignerActions/formDesignerActionDispatcher';
import ManageServiceConfigurationDispatchers from '../actions/manageServiceConfigurationActions/manageServiceConfigurationActionDispatcher';
import { CollapsableMenuComponent } from '../components/rightDrawerMenu/CollapsableMenuComponent';
import DesignView from './DesignView';
import { Toolbar } from './Toolbar';


export interface IFormDesignerProvidedProps {
  classes: any;
}
export interface IFormDesignerProps extends IFormDesignerProvidedProps {
  language: any;
}

type LogicMode = 'Calculation' | 'Dynamics' | 'Validation' | null;

export interface IFormDesignerState {
  codeEditorOpen: boolean;
  codeEditorMode: LogicMode;
}

const styles = ((theme: Theme) => createStyles({
  root: {
    flexGrow: 1,
    minHeight: 'calc(100vh - 69px)',
  },
  button: {
    position: 'relative',
    zIndex: 9999,
  },
  container: {
    height: 'calc(100vh - 69px)',
    top: '69px',
    backgroundColor: altinnTheme.altinnPalette.primary.greyLight,
  },
  devider: {
    width: '100%',
    height: '0.1rem',
    background: altinnTheme.altinnPalette.primary.greyMedium,
  },
  item: {
    padding: 0,
    minWidth: '171px', /* Two columns at 1024px screen size */
  },
  mainContent: {
    borderLeft: '1px solid #C9C9C9',
    borderRight: '1px solid #C9C9C9',
    minWidth: '682px !important', /* Eight columns at 1024px screen size */
    overflowY: 'auto',
  },
  menuHeader: {
    padding: '2.5rem 2.5rem 1.2rem 2.5rem',
    margin: 0,
  },
  fullWidth: {
    width: '100%',
  },
}));
export enum LayoutItemType {
  Container = 'CONTAINER',
  Component = 'COMPONENT',
}

class FormDesigner extends React.Component<
  IFormDesignerProps,
  IFormDesignerState
  > {

  constructor(props: IFormDesignerProps) {
    super(props);
    this.state = {
      codeEditorOpen: false,
      codeEditorMode: null,
    };
  }

  public componentDidMount() {
    const altinnWindow: IAltinnWindow = window as IAltinnWindow;
    const { org, service } = altinnWindow;
    const servicePath = `${org}/${service}`;

    FormDesignerActionDispatchers.fetchFormLayout(
      `${altinnWindow.location.origin}/designer/${servicePath}/UIEditor/GetFormLayout`);
    AppDataActionDispatcher.setDesignMode(true);
    ManageServiceConfigurationDispatchers.fetchJsonFile(
      `${altinnWindow.location.origin}/designer/${
      servicePath}/UIEditor/GetJsonFile?fileName=ServiceConfigurations.json`);
  }

  public toggleCodeEditor = (mode?: LogicMode) => {
    this.setState((prevState: IFormDesignerState) => {
      return {
        codeEditorOpen: !prevState.codeEditorOpen,
        codeEditorMode: mode ? mode : null,
      };
    });
  }

  public renderLogicMenu = () => {
    return (
      <Drawer
        anchor='bottom'
        open={this.state.codeEditorOpen}
      >
        <FileEditor
          mode={this.state.codeEditorMode.toString()}
          closeFileEditor={this.toggleCodeEditor}
        />
      </Drawer>
    );
  }

  public render() {
    const { classes } = this.props;
    return (
      <div className={classes.root}>
        <Grid
          container={true}
          wrap={'nowrap'}
          spacing={0}
          classes={{ container: classNames(classes.container) }}
        >
          <Grid item={true} xs={2} classes={{ item: classNames(classes.item) }}>
            <Toolbar />
          </Grid>
          <Grid item={true} xs={8} className={classes.mainContent} classes={{ item: classNames(classes.item) }}>
            <VersionControlHeader language={this.props.language} />
            <div
              style={{
                width: 'calc(100% - 48px)',
                paddingTop: '24px',
                marginLeft: '24px',
              }}
            >
              <DesignView />
              {this.state.codeEditorOpen ?
                this.renderLogicMenu()
              : null}
            </div>
          </Grid>
          <Grid item={true} classes={{ item: classNames(classes.item) }}>
            <ServiceLogicMenu
              open={false}
              button={
                <Grid
                  container={true}
                  direction={'column'}
                  justify={'center'}
                  alignItems={'flex-end'}
                >
                <IconButton
                  type='button'
                  className={this.props.classes.button}
                >
                  <i className='ai ai-plain-circle' />
                </IconButton>
                </Grid>}
            >
              <div className={this.props.classes.fullWidth}>
                <h3 className={this.props.classes.menuHeader}>
                  {this.props.language.ux_editor.service_logic}
                </h3>
                <CollapsableMenuComponent
                  header={this.props.language.ux_editor.service_logic_validations}
                  listItems={[{name: this.props.language.ux_editor.service_logic_edit_validations}]}
                  menuIsOpen={true}
                />
                <CollapsableMenuComponent
                  header={this.props.language.ux_editor.service_logic_dynamics}
                  listItems={[
                    {
                      name: this.props.language.ux_editor.service_logic_edit_dynamics,
                      action: this.toggleCodeEditor.bind(this, 'Dynamics'),
                    }]}
                  menuIsOpen={true}
                />
                <CollapsableMenuComponent
                  header={this.props.language.ux_editor.service_logic_calculations}
                  listItems={[{name: this.props.language.ux_editor.service_logic_edit_calculations}]}
                  menuIsOpen={true}
                />
                <div className={this.props.classes.devider}/>
              </div>
            </ServiceLogicMenu >
          </Grid>
        </Grid>
      </div>
    );
  }
}

const mapsStateToProps = (
  state: IAppState,
  props: IFormDesignerProvidedProps,
): IFormDesignerProps => {
  return {
    classes: props.classes,
    language: state.appData.language.language,
  };
};

export default withStyles(
  styles,
  { withTheme: true },
)(
  connect(
    mapsStateToProps,
  )(
    DragDropContext(
      HTML5Backend,
    )(
      FormDesigner,
    ),
  ),
);