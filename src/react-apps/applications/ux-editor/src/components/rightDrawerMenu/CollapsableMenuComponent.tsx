import { createStyles, List, ListItem, ListItemIcon, ListItemText, withStyles } from '@material-ui/core';
import * as React from 'react';
import { connect } from 'react-redux';
import altinnTheme from '../../../../shared/src/theme/altinnStudioTheme';

const styles = createStyles({
  collapseHeader: {
    margin: '0 !important',
    padding: '0 !important',
  },
  icon: {
    padding: '0 0.6rem',
    width: '2.5rem !important',
    fontSize: '3rem',
  },
  link: {
    textDecoration: 'underline',
    textDecorationColor: altinnTheme.altinnPalette.primary.blueDark,
  },
  list: {
    padding: 0,
  },
  listItemHeader: {
    padding: '1.2rem 0',
    borderTop: '1px solid ' + altinnTheme.altinnPalette.primary.greyMedium,
  },
  listItem: {
    width: '100%',
    color: altinnTheme.altinnPalette.primary.blueDarker,
    fontSize: '1.6rem',
  },
  rotateDown: {
    transform: 'rotate(90deg)',
    fontSize: '1.3rem',
    margin: '0 !important',
  },
  rotateRight: {
    fontSize: '1.3rem',
    margin: '0 !important',
  },
});
export interface ICollapsableMenuProvidedProps {
  classes: any;
  header: string;
  listItems: any[];
  menuIsOpen: boolean;
}

export interface ICollapsableMenuProps extends ICollapsableMenuProvidedProps {
  language: any;
}

export interface ICollapsableMenuState {
  showContent: boolean;
  menuIsOpen: boolean;
}

class CollapsableMenu extends React.Component<ICollapsableMenuProps, ICollapsableMenuState> {
  constructor(_props: ICollapsableMenuProps, _state: ICollapsableMenuState) {
    super(_props, _state);
    this.state = {
      showContent: false,
      menuIsOpen: _props.menuIsOpen,
    };
  }
  public toggleMenu = () => {
    this.setState({
      menuIsOpen: !this.state.menuIsOpen,
    });
  }
  public showContent = () => {
    this.setState((prevState) => ({
      showContent: !prevState.showContent,
    }));
  }
  public render(): JSX.Element {
    return (
      <List className={this.props.classes.list}>
        <ListItem
          className={this.props.classes.listItem + ' ' + this.props.classes.listItemHeader}
        >
          <ListItemIcon
            className={this.state.menuIsOpen ? this.props.classes.rotateDown : this.props.classes.rotateRight}
            onClick={this.toggleMenu}
          >
            <i className={'ai ai-expand ' + this.props.classes.icon} />
          </ListItemIcon>
          <span className={this.props.classes.collapseHeader}>{this.props.header}</span>
        </ListItem>
        {this.state.menuIsOpen && this.props.listItems.map((item, index) => {
          return (
            <>
            <ListItem key={index} className={this.props.classes.listItem}>
              <span
                className={this.props.classes.link}
                onClick={this.showContent}
              >
                {item.name}
              </span>
            </ListItem>
            {this.state.showContent &&
              <ListItem>
                here comes content
              </ListItem>
            }
            </>
          );
        })}
      </List>
    );
  }
}

const mapStateToProps: (
  state: IAppState,
  props: ICollapsableMenuProvidedProps,
) => ICollapsableMenuProps = (state: IAppState, props: ICollapsableMenuProvidedProps) => ({
  classes: props.classes,
  header: props.header,
  language: state.appData.language.language,
  listItems: props.listItems,
  menuIsOpen: props.menuIsOpen,
});

export const CollapsableMenuComponent =
  withStyles(styles, { withTheme: true })(connect(mapStateToProps)(CollapsableMenu));
