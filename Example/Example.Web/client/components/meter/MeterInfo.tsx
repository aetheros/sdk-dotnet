import { List, ListItem } from "@material-ui/core";
import ListSubheader from '@material-ui/core/ListSubheader';
import Paper from "@material-ui/core/Paper";
import * as React from "react";

const MeterEvents = props => {
	return (
		<Paper>
			<ListSubheader>Info</ListSubheader>
			<List>
				<ListItem>{"ID: " + props.meterId}</ListItem>
			</List>
		</Paper>
	);
};

export default MeterEvents;