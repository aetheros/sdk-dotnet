import { List, ListItem, ListItemText, Card, CardHeader, CardContent } from "@material-ui/core";
import ListSubheader from '@material-ui/core/ListSubheader';
import Paper from "@material-ui/core/Paper";
import { createStyles, Theme, WithStyles, withStyles } from '@material-ui/core/styles';
import * as React from "react";


const styles = (theme: Theme) => createStyles({
	subheader: {
	},
	div: {
		marginLeft: "auto",
		marginRight: "auto",
	},
});

interface Props extends WithStyles<typeof styles> {
	meterState: string;
	meterId: string;
};

type State = {
	openValve: boolean;
};

class MeterDetailComponent extends React.Component<Props, State> {

	constructor(props: Props) {
		super(props);
		this.state = {
			openValve: true
		};
	}

	render() {

		return (
			<List dense={true} subheader={<ListSubheader>Meter Details</ListSubheader>}>
				<ListItem>
					<ListItemText primary={`Meter ID: ${this.props.meterId}`} />
				</ListItem>
				<ListItem>
					<ListItemText primary={`Water Main Valve: ${this.props.meterState}`} />
				</ListItem>
			</List>
		);
	}
};

export default Object.assign(withStyles(styles)(MeterDetailComponent), { name: '' });