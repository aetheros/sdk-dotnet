import { List, ListItem, ListItemSecondaryAction, ListItemText } from "@material-ui/core";
import { cyan } from "@material-ui/core/colors";
import Fab from "@material-ui/core/Fab";
import ListSubheader from '@material-ui/core/ListSubheader';
import Paper from "@material-ui/core/Paper";
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import AddIcon from "@material-ui/icons/Add";
import * as React from "react";

const styles = (theme: Theme) => createStyles({
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

export type MeterCommandModel = {
	Action: string;
	When: string;
};

interface Props extends WithStyles<typeof styles> {
	meterCommand: MeterCommandModel;
	handleCommandClick: any;
};

type State = {
	openValve: boolean;
};

class MeterCommandComponent extends React.Component<Props, State> {

	constructor(props: Props) {
		super(props);
	}

	render() {
		const classes = this.props.classes;

		const lastCommand = !this.props.meterCommand ? "No Command Found" : `Last Command: ${this.props.meterCommand.Action}`;
		const lastWhen = !this.props.meterCommand ? "" : this.props.meterCommand.When;

		return (
			<List dense={true} subheader={<ListSubheader>Water Main Valve Control</ListSubheader>}>
				<ListItem>
					<ListItemText primary={lastCommand} secondary={lastWhen} />
				</ListItem>
				<ListItemSecondaryAction>
					<Fab
						/*variant="extended"*/
						//mini={true}
						size="small"
						aria-label="Add"
						onClick={this.props.handleCommandClick}>
						<AddIcon />
					</Fab>
				</ListItemSecondaryAction>
			</List>
		);
	}
};
export const MeterCommand = Object.assign(withStyles(styles)(MeterCommandComponent), { name: '' });
