import AppBar from '@material-ui/core/AppBar';
import blue from '@material-ui/core/colors/blue';
import IconButton from '@material-ui/core/IconButton';
import Menu from '@material-ui/core/Menu';
import MenuItem from '@material-ui/core/MenuItem';
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import Toolbar from '@material-ui/core/Toolbar';
import MenuIcon from '@material-ui/icons/Menu';
import MoreVertIcon from '@material-ui/icons/MoreVert';
import * as React from 'react';
import auth from '../auth';

const styles = (theme: Theme) => createStyles({
	root: {
		flexGrow: 1
	},
	appBar: {
		backgroundColor: blue[600],
		overflow: 'hidden',
		position: 'fixed',
		top: 0,
		maxHeight: 56
	},
	menuButton: {
		marginLeft: -24
	},
	morebutton: {
		color: 'white'
	},
	title: {
		flexGrow: 1
	}
});

interface Props extends WithStyles<typeof styles> {
	onSidebarToggle: (evt: any) => void;
	title?: string;
};

type State = {
	anchorEl: Element;
};

class HeaderComponent extends React.Component<Props, State> {

	constructor(props: Props) {
		super(props);
		this.state = {
			anchorEl: null,
		};
	}

	handleIconClick(event) {
		this.setState({ anchorEl: event.currentTarget });
	}
	handleMenuClose() {
		this.setState({ anchorEl: null });
	}
	handleMenuClick() {
		auth.signOut();
	}

	render() {
		const { classes, onSidebarToggle } = this.props;

		return (
			<div className={classes.root}>
				<AppBar classes={classes} className={classes.appBar}>
					<Toolbar>
						<IconButton edge="start" className={classes.menuButton} color="inherit" onClick={onSidebarToggle}>
							<MenuIcon />
						</IconButton>
						<h5 className={classes.title}>{this.props.title}</h5>
						<div>
							<IconButton onClick={this.handleIconClick.bind(this)} color="inherit">
								<MoreVertIcon />
							</IconButton>
							<Menu anchorEl={this.state.anchorEl} open={!!this.state.anchorEl} onClose={this.handleMenuClose.bind(this)}>
								<MenuItem onClick={this.handleMenuClick.bind(this)}>Logout</MenuItem>
							</Menu>
						</div>
					</Toolbar>
				</AppBar>
			</div>
		);
	}
}
export default Object.assign(withStyles(styles)(HeaderComponent), { name: '' });
