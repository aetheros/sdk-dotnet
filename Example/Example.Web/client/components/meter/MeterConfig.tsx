import { List, ListItem, ListItemSecondaryAction, ListItemText } from "@material-ui/core";
import { cyan } from "@material-ui/core/colors";
import IconButton from "@material-ui/core/IconButton";
import ListSubheader from '@material-ui/core/ListSubheader';
import Paper from "@material-ui/core/Paper";
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import SettingsIcon from "@material-ui/icons/Settings";
import * as React from "react";

const styles = (theme: Theme) => createStyles({
	panelButton: {
		position: 'absolute',
		top: theme.spacing(1),
		right: theme.spacing(1),
	},
	subheader: {
		fontSize: 24,
		fontWeight: 'lighter',
		backgroundColor: cyan[600],
		color: "white"
	},
	div: {
		marginLeft: "auto",
		marginRight: "auto",
	},
});

interface Props extends WithStyles<typeof styles> {
	meterReadPolicy: MeterReadPolicy;
	handlePolicyConfigClick: React.ReactEventHandler;
};

type State = {
	openValve: boolean;
};


export type MeterReadPolicy = {
	id: string;
	name: string;
	start: string;
	end: string;
	readInterval: string;
};


class MeterConfigComponent extends React.Component<Props, State> {

	constructor(props: Props) {
		super(props);
		this.state = {
			openValve: false,
		};
	}

	render() {

		const currentPolicy = !this.props.meterReadPolicy ? "No policy found" : `Collect Usage Reads every ${this.props.meterReadPolicy.readInterval}`;
		return (
			<List dense={true} subheader={
				<ListSubheader>
					Policy
					<IconButton
						size="small"
						aria-label="Add"
						onClick={this.props.handlePolicyConfigClick}
						className={this.props.classes.panelButton}
					>
						<SettingsIcon />
					</IconButton>
				</ListSubheader>
			}>
				<ListItem>
					<ListItemText primary={currentPolicy} />
				</ListItem>
			</List>
		);
	}
};

export const MeterConfig = Object.assign(withStyles(styles)(MeterConfigComponent), { name: '' });
